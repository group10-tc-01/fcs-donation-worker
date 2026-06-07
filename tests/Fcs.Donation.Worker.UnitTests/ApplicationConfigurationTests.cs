using Fcs.Donation.Worker.Application.Common.Services;
using Fcs.Donation.Worker.Application.DependencyInjection;
using Fcs.Donation.Worker.Application.Features.SampleEvent;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fcs.Donation.Worker.UnitTests;

public sealed class ApplicationConfigurationTests
{
    [Fact]
    public void Given_AddApplication_When_ConfigurationIsValid_Then_ShouldRegisterWorkerServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaSettings:BootstrapServers"] = "localhost:9092",
                ["KafkaSettings:GroupId"] = "fcs-donation-worker-consumer-group",
                ["KafkaSettings:Topics:SampleEvent"] = "donation-received"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddApplication(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<SampleNotificationService>().Should().NotBeNull();
        serviceProvider.GetServices<IHostedService>().Should().ContainSingle().Which.Should().BeOfType<SampleEventConsumer>();
    }

    [Fact]
    public async Task Given_SendAsync_When_Called_Then_ShouldCompleteSuccessfully()
    {
        var service = new SampleNotificationService(NullLogger<SampleNotificationService>.Instance);

        var act = () => service.SendAsync("campaigns", "donation processed", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Given_SampleEvent_When_Created_Then_ShouldExposeKafkaPayloadFields()
    {
        var eventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var occurredAt = new DateTimeOffset(2026, 5, 18, 20, 0, 0, TimeSpan.Zero);

        var sampleEvent = new SampleEvent(eventId, "campaigns", "donation processed", occurredAt);

        sampleEvent.Id.Should().Be(eventId);
        sampleEvent.Recipient.Should().Be("campaigns");
        sampleEvent.Message.Should().Be("donation processed");
        sampleEvent.OccurredAt.Should().Be(occurredAt);
    }
}
