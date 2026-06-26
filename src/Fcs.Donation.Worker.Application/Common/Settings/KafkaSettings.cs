using System.Diagnostics.CodeAnalysis;

namespace Fcs.Donation.Worker.Application.Common.Settings;

[ExcludeFromCodeCoverage]
public sealed class KafkaSettings
{
    public const string SectionName = "KafkaSettings";

    public string BootstrapServers { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public int ConsumerTimeoutMs { get; init; } = 100;
    public KafkaTopics Topics { get; init; } = new();
}

[ExcludeFromCodeCoverage]
public sealed class KafkaTopics
{
    public string DonationReceived { get; init; } = string.Empty;
    public string AuditLogRequested { get; init; } = string.Empty;
}
