using System.Diagnostics.CodeAnalysis;

namespace Fcs.Donation.Worker.Application.Features.DonationReceived.Http;

[ExcludeFromCodeCoverage]
public sealed record CampaignsApiResponse<T>(bool Success, T? Data, string? Message);
