using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Fcs.Donation.Worker.Application.Common.Abstractions;

[ExcludeFromCodeCoverage]
public abstract class BaseKafkaConsumer<TEvent> : BackgroundService where TEvent : class
{
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

            var @event = JsonSerializer.Deserialize<TEvent>(consumeResult.Message.Value);

            if (@event is null)
            {
                _logger.LogWarning("Ignoring empty or invalid message from topic {Topic}", _topic);
                return;
            }

            await ProcessEventAsync(@event, cancellationToken);
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

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}
