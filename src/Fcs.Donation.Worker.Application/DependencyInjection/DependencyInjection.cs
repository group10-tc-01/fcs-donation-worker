using Fcs.Donation.Worker.Application.Common.Settings;
using Fcs.Donation.Worker.Application.Features.DonationReceived.Audit;
using Fcs.Donation.Worker.Application.Features.DonationReceived.Http;
using Fcs.Donation.Worker.Application.Features.DonationReceived.Kafka;
using Fcs.Donation.Worker.Application.Features.DonationReceived.Services;
using Fcs.Donation.Worker.Application.Features.DonationReceived.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Refit;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Fcs.Donation.Worker.Application.DependencyInjection;

[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KafkaSettings>(configuration.GetSection(KafkaSettings.SectionName));
        services
            .AddOptions<CampaignsApiSettings>()
            .Bind(configuration.GetRequiredSection(CampaignsApiSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<DonationsDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("SqlServer")));

        services.AddHealthChecks().AddDbContextCheck<DonationsDbContext>("sqlserver");

        services.AddScoped<IDonationProcessingRepository, SqlServerDonationProcessingRepository>();
        services.AddScoped<DonationProcessingService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAuditPublisher, KafkaAuditPublisher>();
        services.AddHostedService<DonationReceivedEventConsumer>();
        services.AddCampaignsClient();

        return services;
    }

    private static IServiceCollection AddCampaignsClient(this IServiceCollection services)
    {
        services.AddRefitClient<ICampaignsClient>()
            .ConfigureHttpClient((serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<CampaignsApiSettings>>().Value;
                client.BaseAddress = new Uri(settings.BaseUrl);
            })
            .AddPolicyHandler((serviceProvider, _) =>
            {
                var retry = serviceProvider.GetRequiredService<IOptions<CampaignsApiSettings>>().Value.Retry;
                return CreateCampaignsRetryPolicy(retry);
            });

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateCampaignsRetryPolicy(CampaignsApiRetrySettings retry)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retry.RetryCount,
                attempt => TimeSpan.FromMilliseconds(retry.BaseDelayMilliseconds * Math.Pow(2, attempt - 1)));
    }
}
