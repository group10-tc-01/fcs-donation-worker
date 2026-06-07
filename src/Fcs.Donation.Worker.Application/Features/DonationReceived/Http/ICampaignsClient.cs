using Refit;

namespace Fcs.Donation.Worker.Application.Features.DonationReceived.Http;

public interface ICampaignsClient
{
    [Post("/internal/campaigns/{campaignId}/donation-processed")]
    Task<ApiResponse<CampaignsApiResponse<ProcessDonationResponse>>> ProcessDonationAsync(
        Guid campaignId,
        [Body] ProcessDonationRequest request,
        CancellationToken cancellationToken);
}
