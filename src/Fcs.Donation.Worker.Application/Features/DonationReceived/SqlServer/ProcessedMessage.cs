using System.Diagnostics.CodeAnalysis;

namespace Fcs.Donation.Worker.Application.Features.DonationReceived.SqlServer;

[ExcludeFromCodeCoverage]
public sealed class ProcessedMessage
{
    private ProcessedMessage()
    {
    }

    public ProcessedMessage(Guid id, Guid messageId, string topic, DateTime processedAt)
    {
        Id = id;
        MessageId = messageId;
        Topic = topic;
        ProcessedAt = processedAt;
    }

    public Guid Id { get; private set; }
    public Guid MessageId { get; private set; }
    public string Topic { get; private set; } = string.Empty;
    public DateTime ProcessedAt { get; private set; }
}
