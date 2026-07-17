using Fcs.Donation.Worker.Application.Common.Messaging;
using Fcs.Donation.Worker.Application.Common.Settings;
using Fcs.Donation.Worker.Application.Features.DonationReceived.Audit;
using Fcs.Donation.Worker.Application.Features.DonationReceived.Events;
using Fcs.Donation.Worker.Application.Features.DonationReceived.Http;
using Fcs.Donation.Worker.Application.Features.DonationReceived.Notifications;
using Fcs.Donation.Worker.Application.Features.DonationReceived.Services;
using Fcs.Donation.Worker.Application.Features.DonationReceived.SqlServer;
using FluentAssertions;
using Refit;
using Xunit;
using DonationEntity = Fcs.Donation.Worker.Application.Features.DonationReceived.SqlServer.Donation;

namespace Fcs.Donation.Worker.UnitTests;

public sealed class DonationProcessingServiceTests
{
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DonationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CampaignId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid DonorId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTime OccurredAt = new(2026, 5, 18, 20, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime NotificationPublishedAt = new(2026, 5, 18, 20, 1, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Given_PendingDonation_When_ProcessAsyncIsCalled_Then_ShouldReflectCampaignMarkProcessedAndRecordMessage()
    {
        var donation = DonationEntity.CreatePending(DonationId, CampaignId, DonorId, 100m, OccurredAt);
        var repository = new InMemoryDonationProcessingRepository(donation);
        var campaignsClient = new FakeCampaignsClient();
        var messagePublisher = new FakeMessagePublisher();
        var service = CreateService(repository, campaignsClient, messagePublisher);

        await service.ProcessAsync(CreateEvent(), CancellationToken.None);

        var campaignRequest = campaignsClient.Requests.Should().ContainSingle().Subject;
        campaignRequest.CampaignId.Should().Be(CampaignId);
        campaignRequest.Request.Should().Be(new ProcessDonationRequest(DonationId, 100m, OccurredAt));
        donation.Status.Should().Be(DonationStatus.Processed);
        donation.ProcessedAt.Should().Be(OccurredAt);
        donation.FailureReason.Should().BeNull();
        repository.ProcessedMessages.Should().ContainSingle(message => message.MessageId == EventId && message.Topic == DonationProcessingService.DonationReceivedTopic);
        repository.SaveChangesCount.Should().Be(1);
        messagePublisher.AuditEvents.Should().ContainSingle(e => e.Event.Action == AuditActions.DonationProcessed);
    }

    [Fact]
    public async Task Given_PendingDonation_When_ProcessAsyncSucceeds_Then_ShouldPublishDonationProcessedNotificationToRecipient()
    {
        var donation = DonationEntity.CreatePending(DonationId, CampaignId, DonorId, 100m, OccurredAt);
        var repository = new InMemoryDonationProcessingRepository(donation);
        var campaignsClient = new FakeCampaignsClient();
        var messagePublisher = new FakeMessagePublisher();
        var service = CreateService(repository, campaignsClient, messagePublisher, new FixedTimeProvider(NotificationPublishedAt));

        await service.ProcessAsync(CreateEvent(), CancellationToken.None);

        var notification = messagePublisher.NotificationEvents.Should().ContainSingle().Subject;
        notification.TopicName.Should().Be(KafkaTopicKeys.EmailNotification);
        notification.Event.Type.Should().Be(EmailNotificationRequestedEvent.DonationProcessed);
        notification.Event.RecipientEmail.Should().Be("doador@teste.local");
        notification.Event.DonationId.Should().Be(DonationId);
        notification.Event.Amount.Should().Be(100m);
        notification.Event.OccurredAt.Should().Be(NotificationPublishedAt);
    }

    [Fact]
    public async Task Given_NotificationPublishingFails_When_ProcessAsyncSucceeds_Then_ShouldKeepDonationProcessed()
    {
        var donation = DonationEntity.CreatePending(DonationId, CampaignId, DonorId, 100m, OccurredAt);
        var repository = new InMemoryDonationProcessingRepository(donation);
        var campaignsClient = new FakeCampaignsClient();
        var messagePublisher = new FakeMessagePublisher { ThrowOnTopic = KafkaTopicKeys.EmailNotification };
        var service = CreateService(repository, campaignsClient, messagePublisher);

        var action = () => service.ProcessAsync(CreateEvent(), CancellationToken.None);

        await action.Should().NotThrowAsync();
        donation.Status.Should().Be(DonationStatus.Processed);
        repository.SaveChangesCount.Should().Be(1);
        messagePublisher.AuditEvents.Should().ContainSingle(e => e.Event.Action == AuditActions.DonationProcessed);
        messagePublisher.NotificationPublishAttempts.Should().Be(1);
    }

    [Fact]
    public async Task Given_DuplicateMessage_When_ProcessAsyncIsCalled_Then_ShouldNotCallCampaignsAndShouldPublishDuplicateAudit()
    {
        var donation = DonationEntity.CreatePending(DonationId, CampaignId, DonorId, 100m, OccurredAt);
        var repository = new InMemoryDonationProcessingRepository(donation);
        repository.ProcessedMessages.Add(new ProcessedMessage(Guid.NewGuid(), EventId, DonationProcessingService.DonationReceivedTopic, OccurredAt));
        var campaignsClient = new FakeCampaignsClient();
        var messagePublisher = new FakeMessagePublisher();
        var service = CreateService(repository, campaignsClient, messagePublisher);

        await service.ProcessAsync(CreateEvent(), CancellationToken.None);

        campaignsClient.Requests.Should().BeEmpty();
        donation.Status.Should().Be(DonationStatus.Pending);
        repository.SaveChangesCount.Should().Be(0);
        messagePublisher.AuditEvents.Should().ContainSingle(e => e.Event.Action == AuditActions.DuplicateMessageIgnored);
    }

    [Fact]
    public async Task Given_DonationNotFound_When_ProcessAsyncIsCalled_Then_ShouldRecordMessageAndPublishDonationFailedAudit()
    {
        var repository = new InMemoryDonationProcessingRepository();
        var campaignsClient = new FakeCampaignsClient();
        var messagePublisher = new FakeMessagePublisher();
        var service = CreateService(repository, campaignsClient, messagePublisher);

        await service.ProcessAsync(CreateEvent(), CancellationToken.None);

        campaignsClient.Requests.Should().BeEmpty();
        var donation = await repository.GetDonationAsync(DonationId, CancellationToken.None);
        donation.Should().BeNull();
        repository.SaveChangesCount.Should().Be(1);
        repository.ProcessedMessages.Should().ContainSingle(m => m.MessageId == EventId);
        var auditEvent = messagePublisher.AuditEvents.Should().ContainSingle(e => e.Event.Action == AuditActions.DonationFailed).Subject.Event;
        auditEvent.Metadata.Should().Contain(pair => pair.Key == "failureReason" && (string?)pair.Value == "Donation not found.");
    }

    [Fact]
    public async Task Given_DonationAlreadyProcessedWithoutLedger_When_ProcessAsyncIsCalled_Then_ShouldRecordMessageAndPublishDuplicateAudit()
    {
        var donation = DonationEntity.CreatePending(DonationId, CampaignId, DonorId, 100m, OccurredAt);
        donation.MarkProcessed(OccurredAt);
        var repository = new InMemoryDonationProcessingRepository(donation);
        var campaignsClient = new FakeCampaignsClient();
        var messagePublisher = new FakeMessagePublisher();
        var service = CreateService(repository, campaignsClient, messagePublisher);

        await service.ProcessAsync(CreateEvent(), CancellationToken.None);

        campaignsClient.Requests.Should().BeEmpty();
        repository.ProcessedMessages.Should().ContainSingle(m => m.MessageId == EventId);
        repository.SaveChangesCount.Should().Be(1);
        messagePublisher.AuditEvents.Should().ContainSingle(e => e.Event.Action == AuditActions.DuplicateMessageIgnored);
    }

    [Fact]
    public async Task Given_DonationAlreadyFailedWithoutLedger_When_ProcessAsyncIsCalled_Then_ShouldRecordMessageAndPublishDonationFailedAudit()
    {
        var donation = DonationEntity.CreatePending(DonationId, CampaignId, DonorId, 100m, OccurredAt);
        donation.MarkFailed("Previous failure.", OccurredAt);
        var repository = new InMemoryDonationProcessingRepository(donation);
        var campaignsClient = new FakeCampaignsClient();
        var messagePublisher = new FakeMessagePublisher();
        var service = CreateService(repository, campaignsClient, messagePublisher);

        await service.ProcessAsync(CreateEvent(), CancellationToken.None);

        campaignsClient.Requests.Should().BeEmpty();
        repository.ProcessedMessages.Should().ContainSingle(m => m.MessageId == EventId);
        repository.SaveChangesCount.Should().Be(1);
        var auditEvent = messagePublisher.AuditEvents.Should().ContainSingle(e => e.Event.Action == AuditActions.DonationFailed).Subject.Event;
        auditEvent.Metadata.Should().Contain(pair => pair.Key == "failureReason" && (string?)pair.Value == "Donation is not pending. Current status: Failed.");
    }

    [Fact]
    public async Task Given_CampaignsApiFails_When_ProcessAsyncIsCalled_Then_ShouldMarkDonationFailedAndPublishAudit()
    {
        var donation = DonationEntity.CreatePending(DonationId, CampaignId, DonorId, 100m, OccurredAt);
        var repository = new InMemoryDonationProcessingRepository(donation);
        var campaignsClient = new FakeCampaignsClient { ThrowOnProcess = true };
        var messagePublisher = new FakeMessagePublisher();
        var service = CreateService(repository, campaignsClient, messagePublisher);

        await service.ProcessAsync(CreateEvent(), CancellationToken.None);

        donation.Status.Should().Be(DonationStatus.Failed);
        donation.ProcessedAt.Should().NotBeNull();
        donation.FailureReason.Should().Contain("Campaign update failed");
        repository.ProcessedMessages.Should().ContainSingle(m => m.MessageId == EventId);
        repository.SaveChangesCount.Should().Be(1);
        messagePublisher.AuditEvents.Should().ContainSingle(e => e.Event.Action == AuditActions.DonationFailed);
        messagePublisher.NotificationEvents.Should().BeEmpty();
    }

    private static DonationProcessingService CreateService(
        IDonationProcessingRepository repository,
        ICampaignsClient campaignsClient,
        IMessagePublisher messagePublisher,
        TimeProvider? timeProvider = null)
    {
        return new DonationProcessingService(repository, campaignsClient, messagePublisher, timeProvider ?? TimeProvider.System);
    }

    private static DonationReceivedEvent CreateEvent()
    {
        return new DonationReceivedEvent(EventId, DonationId, CampaignId, DonorId, 100m, OccurredAt, "doador@teste.local");
    }

    private sealed class InMemoryDonationProcessingRepository : IDonationProcessingRepository
    {
        private readonly Dictionary<Guid, DonationEntity> _donations;

        public InMemoryDonationProcessingRepository(params DonationEntity[] donations)
        {
            _donations = donations.ToDictionary(donation => donation.Id);
        }

        public List<ProcessedMessage> ProcessedMessages { get; } = [];
        public int SaveChangesCount { get; private set; }

        public Task<bool> HasProcessedMessageAsync(Guid messageId, string topic, CancellationToken cancellationToken)
        {
            return Task.FromResult(ProcessedMessages.Any(message => message.MessageId == messageId && message.Topic == topic));
        }

        public Task<DonationEntity?> GetDonationAsync(Guid donationId, CancellationToken cancellationToken)
        {
            _donations.TryGetValue(donationId, out var donation);
            return Task.FromResult(donation);
        }

        public Task AddProcessedMessageAsync(ProcessedMessage processedMessage, CancellationToken cancellationToken)
        {
            ProcessedMessages.Add(processedMessage);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCampaignsClient : ICampaignsClient
    {
        public List<(Guid CampaignId, ProcessDonationRequest Request)> Requests { get; } = [];
        public bool ThrowOnProcess { get; init; }

        public Task<Refit.ApiResponse<CampaignsApiResponse<ProcessDonationResponse>>> ProcessDonationAsync(
            Guid campaignId,
            ProcessDonationRequest request,
            CancellationToken cancellationToken)
        {
            if (ThrowOnProcess)
            {
                throw new InvalidOperationException("Campaign service unavailable.");
            }

            Requests.Add((campaignId, request));
            return Task.FromResult(new Refit.ApiResponse<CampaignsApiResponse<ProcessDonationResponse>>(
                new HttpResponseMessage(System.Net.HttpStatusCode.OK),
                new CampaignsApiResponse<ProcessDonationResponse>(true, new ProcessDonationResponse(campaignId, request.DonationId, true, false), null),
                new RefitSettings()));
        }
    }

    private sealed class FakeMessagePublisher : IMessagePublisher
    {
        public List<(string TopicName, AuditLogRequestedEvent Event)> AuditEvents { get; } = [];
        public List<(string TopicName, EmailNotificationRequestedEvent Event)> NotificationEvents { get; } = [];
        public int NotificationPublishAttempts { get; private set; }
        public string? ThrowOnTopic { get; init; }

        public Task PublishAsync<TMessage>(string topicName, TMessage message, CancellationToken cancellationToken = default)
        {
            if (topicName == KafkaTopicKeys.EmailNotification)
            {
                NotificationPublishAttempts++;
            }

            if (topicName == ThrowOnTopic)
            {
                throw new InvalidOperationException("Notification service unavailable.");
            }

            if (message is AuditLogRequestedEvent auditEvent)
            {
                AuditEvents.Add((topicName, auditEvent));
            }

            if (message is EmailNotificationRequestedEvent notificationEvent)
            {
                NotificationEvents.Add((topicName, notificationEvent));
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
