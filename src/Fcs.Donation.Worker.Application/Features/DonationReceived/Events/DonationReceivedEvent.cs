using System.Diagnostics.CodeAnalysis;

namespace Fcs.Donation.Worker.Application.Features.DonationReceived.Events;

[ExcludeFromCodeCoverage]
public sealed record DonationReceivedEvent(
    Guid EventId,
    Guid DonationId,
    Guid CampaignId,
    Guid DonorId,
    decimal Amount,
    DateTime OccurredAt);
