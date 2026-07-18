using Confluent.Kafka;
using Fcs.Donation.Worker.Application.Common.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Fcs.Donation.Worker.Application.Common.Messaging;

[ExcludeFromCodeCoverage]
public sealed class KafkaMessagePublisher : IMessagePublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaMessagePublisher> _logger;

    public KafkaMessagePublisher(IOptions<KafkaSettings> options, ILogger<KafkaMessagePublisher> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync<TMessage>(string topicName, TMessage message, CancellationToken cancellationToken = default)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            Acks = Acks.All
        };

        using var producer = new ProducerBuilder<Null, string>(config).Build();
        var payload = JsonSerializer.Serialize(message, SerializerOptions);
        var resolvedTopicName = ResolveTopicName(topicName);
        await producer.ProduceAsync(resolvedTopicName, CreateMessage(payload), cancellationToken);
        _logger.LogInformation("Published message to topic {TopicName}", resolvedTopicName);
    }

    private static Message<Null, string> CreateMessage(string payload)
    {
        var headers = new Headers();
        var activity = Activity.Current;

        if (activity?.Id is { } traceParent)
        {
            headers.Add("traceparent", Encoding.UTF8.GetBytes(traceParent));
        }

        if (!string.IsNullOrWhiteSpace(activity?.TraceStateString))
        {
            headers.Add("tracestate", Encoding.UTF8.GetBytes(activity.TraceStateString));
        }

        return new Message<Null, string> { Value = payload, Headers = headers };
    }

    private string ResolveTopicName(string topicKey)
    {
        return topicKey switch
        {
            KafkaTopicKeys.AuditLog => _settings.Topics.AuditLog,
            KafkaTopicKeys.EmailNotification => _settings.Topics.EmailNotification,
            _ => throw new ArgumentOutOfRangeException(nameof(topicKey), topicKey, "Unknown Kafka topic key.")
        };
    }
}
