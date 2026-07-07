using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace Fcs.Donation.Worker.Application.Features.DonationReceived.SqlServer;

[ExcludeFromCodeCoverage]
public sealed class SqlServerDonationProcessingRepository : IDonationProcessingRepository
{
    private readonly DonationsDbContext _dbContext;

    public SqlServerDonationProcessingRepository(DonationsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> HasProcessedMessageAsync(Guid messageId, string topic, CancellationToken cancellationToken)
    {
        return _dbContext.ProcessedMessages.AnyAsync(
            message => message.MessageId == messageId && message.Topic == topic,
            cancellationToken);
    }

    public Task<Donation?> GetDonationAsync(Guid donationId, CancellationToken cancellationToken)
    {
        return _dbContext.Donations.FirstOrDefaultAsync(donation => donation.Id == donationId, cancellationToken);
    }

    public Task AddProcessedMessageAsync(ProcessedMessage processedMessage, CancellationToken cancellationToken)
    {
        return _dbContext.ProcessedMessages.AddAsync(processedMessage, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
