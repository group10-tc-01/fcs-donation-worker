using Fcs.Donation.Worker.Application.Common.Abstractions;
using Fcs.Donation.Worker.Application.Common.Services;
using Fcs.Donation.Worker.Application.Common.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fcs.Donation.Worker.Application.Features.SampleEvent;

public sealed class SampleEventConsumer : BaseKafkaConsumer<SampleEvent>
{
    private readonly ILogger<SampleEventConsumer> _logger;
    private readonly SampleNotificationService _notificationService;

    public SampleEventConsumer(
        ILogger<SampleEventConsumer> logger,
        IOptions<KafkaSettings> kafkaSettings,
        SampleNotificationService notificationService)
        : base(
            logger,
            kafkaSettings.Value.BootstrapServers,
            kafkaSettings.Value.GroupId,
            kafkaSettings.Value.Topics.SampleEvent,
            kafkaSettings.Value.ConsumerTimeoutMs)
    {
        _logger = logger;
        _notificationService = notificationService;
    }

    protected override async Task ProcessEventAsync(SampleEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing SampleEvent {EventId}", @event.Id);

        await _notificationService.SendAsync(@event.Recipient, @event.Message, cancellationToken);

        _logger.LogInformation("SampleEvent {EventId} processed", @event.Id);
    }
}
