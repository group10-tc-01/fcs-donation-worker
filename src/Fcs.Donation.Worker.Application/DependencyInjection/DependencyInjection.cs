using Fcs.Donation.Worker.Application.Common.Services;
using Fcs.Donation.Worker.Application.Common.Settings;
using Fcs.Donation.Worker.Application.Features.SampleEvent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fcs.Donation.Worker.Application.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KafkaSettings>(configuration.GetSection(KafkaSettings.SectionName));
        services.AddHostedService<SampleEventConsumer>();
        services.AddSingleton<SampleNotificationService>();

        return services;
    }
}
