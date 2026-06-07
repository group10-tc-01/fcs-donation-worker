namespace Fcs.Donation.Worker.Application.Features.DonationReceived.Http;

public sealed record ProcessDonationRequest(Guid DonationId, decimal Amount, DateTime ProcessedAt);
