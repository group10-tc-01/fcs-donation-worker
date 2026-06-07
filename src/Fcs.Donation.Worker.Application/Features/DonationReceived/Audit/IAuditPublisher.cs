namespace Fcs.Donation.Worker.Application.Features.DonationReceived.Audit;

public interface IAuditPublisher
{
    Task PublishAsync(AuditLogRequestedEvent auditEvent, CancellationToken cancellationToken);
}
