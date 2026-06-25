namespace Fcs.Donation.Worker.Application.Features.DonationReceived.SqlServer;

public interface IDonationProcessingRepository
{
    Task<bool> HasProcessedMessageAsync(Guid messageId, string topic, CancellationToken cancellationToken);
    Task<Donation?> GetDonationAsync(Guid donationId, CancellationToken cancellationToken);
    Task AddDonationAsync(Donation donation, CancellationToken cancellationToken);
    Task AddProcessedMessageAsync(ProcessedMessage processedMessage, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
