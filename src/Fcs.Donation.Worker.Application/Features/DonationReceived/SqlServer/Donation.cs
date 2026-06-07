namespace Fcs.Donation.Worker.Application.Features.DonationReceived.SqlServer;

public sealed class Donation
{
    private Donation()
    {
    }

    private Donation(Guid id, Guid campaignId, Guid donorId, decimal amount, DateTime createdAt)
    {
        Id = id;
        CampaignId = campaignId;
        DonorId = donorId;
        Amount = amount;
        Status = DonationStatus.Pending;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid DonorId { get; private set; }
    public decimal Amount { get; private set; }
    public DonationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? FailureReason { get; private set; }

    public static Donation CreatePending(Guid id, Guid campaignId, Guid donorId, decimal amount, DateTime createdAt)
    {
        return new Donation(id, campaignId, donorId, amount, createdAt);
    }

    public void MarkProcessed(DateTime processedAt)
    {
        Status = DonationStatus.Processed;
        ProcessedAt = processedAt;
        FailureReason = null;
    }

    public void MarkFailed(string reason, DateTime processedAt)
    {
        Status = DonationStatus.Failed;
        ProcessedAt = processedAt;
        FailureReason = reason.Length <= 1000 ? reason : reason[..1000];
    }
}
