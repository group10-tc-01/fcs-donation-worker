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
    public string AuditLog { get; init; } = string.Empty;
    public string EmailNotification { get; init; } = string.Empty;
}

public static class KafkaTopicKeys
{
    public const string AuditLog = nameof(KafkaTopics.AuditLog);
    public const string EmailNotification = nameof(KafkaTopics.EmailNotification);
}
