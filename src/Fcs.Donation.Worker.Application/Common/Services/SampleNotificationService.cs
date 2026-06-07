using Microsoft.Extensions.Logging;

namespace Fcs.Donation.Worker.Application.Common.Services;

public sealed class SampleNotificationService
{
    private readonly ILogger<SampleNotificationService> _logger;

    public SampleNotificationService(ILogger<SampleNotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string recipient, string message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sending sample notification to {Recipient}: {Message}", recipient, message);

        return Task.CompletedTask;
    }
}
