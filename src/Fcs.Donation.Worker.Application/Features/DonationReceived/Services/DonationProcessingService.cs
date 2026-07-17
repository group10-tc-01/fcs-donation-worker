using Fcs.Donation.Worker.Application.Common.Messaging;
using Fcs.Donation.Worker.Application.Common.Settings;
using Fcs.Donation.Worker.Application.Features.DonationReceived.Audit;
using Fcs.Donation.Worker.Application.Features.DonationReceived.Events;
using Fcs.Donation.Worker.Application.Features.DonationReceived.Http;
using Fcs.Donation.Worker.Application.Features.DonationReceived.SqlServer;
using Fcs.Donation.Worker.Application.Features.DonationReceived.Notifications;
using Microsoft.Extensions.Logging;

namespace Fcs.Donation.Worker.Application.Features.DonationReceived.Services;

public sealed class DonationProcessingService
{
    public const string DonationReceivedTopic = "donation-received";

    private readonly IDonationProcessingRepository _repository;
    private readonly ICampaignsClient _campaignsClient;
    private readonly IMessagePublisher _messagePublisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DonationProcessingService>? _logger;

    public DonationProcessingService(
        IDonationProcessingRepository repository,
        ICampaignsClient campaignsClient,
        IMessagePublisher messagePublisher,
        TimeProvider timeProvider,
        ILogger<DonationProcessingService>? logger = null)
    {
        _repository = repository;
        _campaignsClient = campaignsClient;
        _messagePublisher = messagePublisher;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ProcessAsync(DonationReceivedEvent @event, CancellationToken cancellationToken)
    {
        if (await _repository.HasProcessedMessageAsync(@event.EventId, DonationReceivedTopic, cancellationToken))
        {
            await PublishDuplicateAudit(@event, cancellationToken);
            return;
        }

        var donation = await _repository.GetDonationAsync(@event.DonationId, cancellationToken);
        if (donation is null)
        {
            var failureReason = "Donation not found.";
            await AddProcessedMessageAsync(@event, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            await PublishFailedAudit(@event, failureReason, cancellationToken);
            return;
        }

        if (donation.Status is not DonationStatus.Pending)
        {
            var failureReason = $"Donation is not pending. Current status: {donation.Status}.";
            await AddProcessedMessageAsync(@event, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);

            if (donation.Status is DonationStatus.Processed)
            {
                await PublishDuplicateAudit(@event, cancellationToken);
                return;
            }

            await PublishFailedAudit(@event, failureReason, cancellationToken);
            return;
        }

        try
        {
            var request = new ProcessDonationRequest(@event.DonationId, @event.Amount, @event.OccurredAt);
            var response = await _campaignsClient.ProcessDonationAsync(@event.CampaignId, request, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content is null || !response.Content.Success)
            {
                var statusCode = response.StatusCode.HasValue ? ((int)response.StatusCode.Value).ToString() : "unknown status";
                throw new InvalidOperationException($"Campaigns API returned {statusCode}.");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var failureReason = $"Campaign update failed: {exception.Message}";
            donation.MarkFailed(failureReason, _timeProvider.GetUtcNow().UtcDateTime);
            await AddProcessedMessageAsync(@event, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            await PublishFailedAudit(@event, failureReason, cancellationToken);
            return;
        }

        await AddProcessedMessageAsync(@event, cancellationToken);
        donation.MarkProcessed(@event.OccurredAt);
        await _repository.SaveChangesAsync(cancellationToken);
        await PublishProcessedAudit(@event, cancellationToken);
        await PublishProcessedNotificationAsync(@event, cancellationToken);
    }

    private async Task PublishProcessedNotificationAsync(DonationReceivedEvent @event, CancellationToken cancellationToken)
    {
        try
        {
            await _messagePublisher.PublishAsync(
                KafkaTopicKeys.EmailNotification,
                new EmailNotificationRequestedEvent(Guid.NewGuid(), EmailNotificationRequestedEvent.DonationProcessed, @event.RecipientEmail, @event.DonationId, @event.Amount, _timeProvider.GetUtcNow().UtcDateTime),
                cancellationToken);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Failed to publish processed donation notification for donation {DonationId}", @event.DonationId);
        }
    }

    private Task AddProcessedMessageAsync(DonationReceivedEvent @event, CancellationToken cancellationToken)
    {
        return _repository.AddProcessedMessageAsync(
            new ProcessedMessage(Guid.NewGuid(), @event.EventId, DonationReceivedTopic, _timeProvider.GetUtcNow().UtcDateTime),
            cancellationToken);
    }

    private Task PublishProcessedAudit(DonationReceivedEvent @event, CancellationToken cancellationToken)
    {
        return _messagePublisher.PublishAsync(KafkaTopicKeys.AuditLog, AuditLogRequestedEvent.Create(
            AuditActions.DonationProcessed,
            "Donation",
            @event.DonationId.ToString(),
            @event.DonorId,
            "Doador",
            BuildMetadata(@event)), cancellationToken);
    }

    private Task PublishDuplicateAudit(DonationReceivedEvent @event, CancellationToken cancellationToken)
    {
        return _messagePublisher.PublishAsync(KafkaTopicKeys.AuditLog, AuditLogRequestedEvent.Create(
            AuditActions.DuplicateMessageIgnored,
            "ProcessedMessage",
            @event.EventId.ToString(),
            null,
            "System",
            BuildMetadata(@event)), cancellationToken);
    }

    private Task PublishFailedAudit(DonationReceivedEvent @event, string reason, CancellationToken cancellationToken)
    {
        var metadata = BuildMetadata(@event).ToDictionary(pair => pair.Key, pair => pair.Value);
        metadata["failureReason"] = reason;

        return _messagePublisher.PublishAsync(KafkaTopicKeys.AuditLog, AuditLogRequestedEvent.Create(
            AuditActions.DonationFailed,
            "Donation",
            @event.DonationId.ToString(),
            @event.DonorId,
            "Doador",
            metadata), cancellationToken);
    }

    private static IReadOnlyDictionary<string, object?> BuildMetadata(DonationReceivedEvent @event)
    {
        return new Dictionary<string, object?>
        {
            ["eventId"] = @event.EventId,
            ["campaignId"] = @event.CampaignId,
            ["donorId"] = @event.DonorId,
            ["amount"] = @event.Amount,
            ["occurredAt"] = @event.OccurredAt
        };
    }
}
