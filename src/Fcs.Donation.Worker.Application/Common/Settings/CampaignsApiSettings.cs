using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Fcs.Donation.Worker.Application.Common.Settings;

[ExcludeFromCodeCoverage]
public sealed class CampaignsApiSettings
{
    public const string SectionName = "CampaignsApi";

    public string BaseUrl { get; init; } = string.Empty;

    public CampaignsApiRetrySettings Retry { get; init; } = new();
}

[ExcludeFromCodeCoverage]
public sealed class CampaignsApiRetrySettings
{
    public int RetryCount { get; init; } = 3;
    public int BaseDelayMilliseconds { get; init; } = 200;
}
