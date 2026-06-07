namespace Fcs.Donation.Worker.Application.Features.DonationReceived.Audit;

public static class AuditActions
{
    public const string DonationProcessed = nameof(DonationProcessed);
    public const string DonationFailed = nameof(DonationFailed);
    public const string DuplicateMessageIgnored = nameof(DuplicateMessageIgnored);
}
