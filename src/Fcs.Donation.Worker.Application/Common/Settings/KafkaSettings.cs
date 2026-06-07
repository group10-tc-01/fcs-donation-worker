namespace Fcs.Donation.Worker.Application.Common.Settings;

public sealed class KafkaSettings
{
    public const string SectionName = "KafkaSettings";

    public string BootstrapServers { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public int ConsumerTimeoutMs { get; init; } = 100;
    public KafkaTopics Topics { get; init; } = new();
}

public sealed class KafkaTopics
{
    public string SampleEvent { get; init; } = string.Empty;
}
