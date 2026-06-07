using Confluent.Kafka;
using Fcs.Donation.Worker.Application.Common.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Fcs.Donation.Worker.Application.Features.DonationReceived.Audit;

[ExcludeFromCodeCoverage]
public sealed class KafkaAuditPublisher : IAuditPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaAuditPublisher> _logger;

    public KafkaAuditPublisher(IOptions<KafkaSettings> options, ILogger<KafkaAuditPublisher> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(AuditLogRequestedEvent auditEvent, CancellationToken cancellationToken)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            Acks = Acks.All
        };

        using var producer = new ProducerBuilder<Null, string>(config).Build();
        var payload = JsonSerializer.Serialize(auditEvent, SerializerOptions);
        await producer.ProduceAsync(_settings.Topics.AuditLogRequested, new Message<Null, string> { Value = payload }, cancellationToken);
        _logger.LogInformation("Published audit event {Action} to topic {Topic}", auditEvent.Action, _settings.Topics.AuditLogRequested);
    }
}
