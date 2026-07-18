using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace Fcs.Donation.Worker.Application.Common.Abstractions;

[ExcludeFromCodeCoverage]
public abstract class BaseKafkaConsumer<TEvent> : BackgroundService where TEvent : class
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly ActivitySource KafkaActivitySource = new("Fcs.Donation.Worker");

    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger _logger;
    private readonly string _topic;
    private readonly int _consumerTimeoutMs;

    protected BaseKafkaConsumer(
        ILogger logger,
        string bootstrapServers,
        string groupId,
        string topic,
        int consumerTimeoutMs = 100)
    {
        _logger = logger;
        _topic = topic;
        _consumerTimeoutMs = consumerTimeoutMs;

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(_topic);
        _logger.LogInformation("Kafka consumer started for topic {Topic}", _topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ConsumeMessageAsync(stoppingToken);
            }
        }
        finally
        {
            _consumer.Close();
        }
    }

    private async Task ConsumeMessageAsync(CancellationToken cancellationToken)
    {
        try
        {
            var consumeResult = _consumer.Consume(TimeSpan.FromMilliseconds(_consumerTimeoutMs));

            if (consumeResult?.Message?.Value is null)
            {
                await Task.Delay(25, cancellationToken);
                return;
            }

            TEvent? @event;

            try
            {
                @event = JsonSerializer.Deserialize<TEvent>(consumeResult.Message.Value, SerializerOptions);
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Discarding invalid JSON message from topic {Topic} at offset {Offset}", _topic, consumeResult.Offset);
                _consumer.Commit(consumeResult);
                return;
            }

            if (@event is null)
            {
                _logger.LogWarning("Discarding empty message from topic {Topic} at offset {Offset}", _topic, consumeResult.Offset);
                _consumer.Commit(consumeResult);
                return;
            }

            try
            {
                using var activity = KafkaActivitySource.StartActivity(
                    $"kafka consume {_topic}",
                    ActivityKind.Consumer,
                    GetParentContext(consumeResult.Message.Headers));

                activity?.SetTag("messaging.system", "kafka");
                activity?.SetTag("messaging.destination.name", _topic);
                activity?.SetTag("messaging.operation.name", "process");

                await ProcessEventAsync(@event, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error processing message from topic {Topic} at offset {Offset}; message was not committed", _topic, consumeResult.Offset);
                _consumer.Seek(consumeResult.TopicPartitionOffset);
                await Task.Delay(1000, cancellationToken);
                return;
            }

            _consumer.Commit(consumeResult);
        }
        catch (ConsumeException exception)
        {
            _logger.LogError(exception, "Error consuming message from topic {Topic}", _topic);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Kafka consumer for topic {Topic} is shutting down", _topic);
        }
    }

    protected abstract Task ProcessEventAsync(TEvent @event, CancellationToken cancellationToken);

    private static ActivityContext GetParentContext(Headers? headers)
    {
        var traceParent = headers?.GetLastBytes("traceparent");

        if (traceParent is null)
        {
            return default;
        }

        var traceState = headers?.FirstOrDefault(header => header.Key == "tracestate")?.GetValueBytes();
        return ActivityContext.TryParse(
            Encoding.UTF8.GetString(traceParent),
            traceState is null ? null : Encoding.UTF8.GetString(traceState),
            out var context)
            ? context
            : default;
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}
