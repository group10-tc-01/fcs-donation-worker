namespace Fcs.Donation.Worker.Application.Features.DonationReceived.Http;

public sealed record ProcessDonationResponse(Guid CampaignId, Guid DonationId, bool Processed, bool Duplicate);
