using Fcs.Donation.Worker.Application.Features.DonationReceived.Audit;
using Fcs.Donation.Worker.Application.Features.DonationReceived.Events;
using Fcs.Donation.Worker.Application.Features.DonationReceived.Http;
using Fcs.Donation.Worker.Application.Features.DonationReceived.SqlServer;
using DonationEntity = Fcs.Donation.Worker.Application.Features.DonationReceived.SqlServer.Donation;

namespace Fcs.Donation.Worker.Application.Features.DonationReceived.Services;

public sealed class DonationProcessingService
{
    public const string DonationReceivedTopic = "donation-received";

    private readonly IDonationProcessingRepository _repository;
    private readonly ICampaignsClient _campaignsClient;
    private readonly IAuditPublisher _auditPublisher;
    private readonly TimeProvider _timeProvider;

    public DonationProcessingService(
        IDonationProcessingRepository repository,
        ICampaignsClient campaignsClient,
        IAuditPublisher auditPublisher,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _campaignsClient = campaignsClient;
        _auditPublisher = auditPublisher;
        _timeProvider = timeProvider;
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
            donation = DonationEntity.CreatePending(
                @event.DonationId, @event.CampaignId, @event.DonorId, @event.Amount, @event.OccurredAt);
            await _repository.AddDonationAsync(donation, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        if (donation.Status is not DonationStatus.Pending)
        {
            await PublishFailedAudit(@event, $"Donation is not pending. Current status: {donation.Status}.", cancellationToken);
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
            await _repository.SaveChangesAsync(cancellationToken);
            await PublishFailedAudit(@event, failureReason, cancellationToken);
            return;
        }

        await _repository.AddProcessedMessageAsync(
            new ProcessedMessage(Guid.NewGuid(), @event.EventId, DonationReceivedTopic, _timeProvider.GetUtcNow().UtcDateTime),
            cancellationToken);
        donation.MarkProcessed(@event.OccurredAt);
        await _repository.SaveChangesAsync(cancellationToken);
        await PublishProcessedAudit(@event, cancellationToken);
    }

    private Task PublishProcessedAudit(DonationReceivedEvent @event, CancellationToken cancellationToken)
    {
        return _auditPublisher.PublishAsync(AuditLogRequestedEvent.Create(
            AuditActions.DonationProcessed,
            "Donation",
            @event.DonationId.ToString(),
            @event.DonorId,
            "Doador",
            BuildMetadata(@event)), cancellationToken);
    }

    private Task PublishDuplicateAudit(DonationReceivedEvent @event, CancellationToken cancellationToken)
    {
        return _auditPublisher.PublishAsync(AuditLogRequestedEvent.Create(
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

        return _auditPublisher.PublishAsync(AuditLogRequestedEvent.Create(
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
