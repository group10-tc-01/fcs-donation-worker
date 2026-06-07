using System.Diagnostics.CodeAnalysis;

namespace Fcs.Donation.Worker.Application.Features.DonationReceived.Audit;

[ExcludeFromCodeCoverage]
public sealed record AuditLogRequestedEvent(
    Guid EventId,
    DateTime OccurredAt,
    string ServiceName,
    string Action,
    string EntityName,
    string? EntityId,
    Guid? ActorId,
    string? ActorType,
    string? CorrelationId = null,
    string? IpAddress = null,
    string? UserAgent = null,
    IReadOnlyDictionary<string, object?>? Metadata = null)
{
    private const string DonationWorkerServiceName = "fcs-donation-worker";

    public static AuditLogRequestedEvent Create(
        string action,
        string entityName,
        string? entityId,
        Guid? actorId,
        string actorType,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        return new AuditLogRequestedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            DonationWorkerServiceName,
            action,
            entityName,
            entityId,
            actorId,
            actorType,
            Metadata: metadata);
    }
}
