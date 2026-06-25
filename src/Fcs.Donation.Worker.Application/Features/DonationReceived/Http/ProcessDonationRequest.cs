namespace Fcs.Donation.Worker.Application.Features.DonationReceived.Http;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed record ProcessDonationRequest(Guid DonationId, decimal Amount, DateTime ProcessedAt);
