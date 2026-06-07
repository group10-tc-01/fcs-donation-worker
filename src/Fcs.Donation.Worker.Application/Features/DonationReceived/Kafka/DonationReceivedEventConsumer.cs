using Fcs.Donation.Worker.Application.Common.Abstractions;
using Fcs.Donation.Worker.Application.Common.Settings;
using Fcs.Donation.Worker.Application.Features.DonationReceived.Events;
using Fcs.Donation.Worker.Application.Features.DonationReceived.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;

namespace Fcs.Donation.Worker.Application.Features.DonationReceived.Kafka;

[ExcludeFromCodeCoverage]
public sealed class DonationReceivedEventConsumer : BaseKafkaConsumer<DonationReceivedEvent>
{
    private readonly DonationProcessingService _processingService;

    public DonationReceivedEventConsumer(
        ILogger<DonationReceivedEventConsumer> logger,
        IOptions<KafkaSettings> kafkaSettings,
        DonationProcessingService processingService)
        : base(
            logger,
            kafkaSettings.Value.BootstrapServers,
            kafkaSettings.Value.GroupId,
            kafkaSettings.Value.Topics.DonationReceived,
            kafkaSettings.Value.ConsumerTimeoutMs)
    {
        _processingService = processingService;
    }

    protected override Task ProcessEventAsync(DonationReceivedEvent @event, CancellationToken cancellationToken)
    {
        return _processingService.ProcessAsync(@event, cancellationToken);
    }
}
