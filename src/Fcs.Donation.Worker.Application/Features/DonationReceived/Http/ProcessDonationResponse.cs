namespace Fcs.Donation.Worker.Application.Features.DonationReceived.Http;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed record ProcessDonationResponse(Guid CampaignId, Guid DonationId, bool Processed, bool Duplicate);
