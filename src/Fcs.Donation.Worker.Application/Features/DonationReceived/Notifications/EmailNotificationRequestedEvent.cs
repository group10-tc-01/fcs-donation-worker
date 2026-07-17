namespace Fcs.Donation.Worker.Application.Features.DonationReceived.Notifications;

public sealed record EmailNotificationRequestedEvent(
    Guid EventId,
    string Type,
    string RecipientEmail,
    Guid? DonationId,
    decimal? Amount,
    DateTime OccurredAt)
{
    public const string DonationProcessed = "DonationProcessed";
}
