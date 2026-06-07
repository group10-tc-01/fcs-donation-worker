using Fcs.Donation.Worker.Application.DependencyInjection;
using Serilog;

namespace Fcs.Donation.Worker.Worker;

public class Program
{
    protected Program() { }

    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddApplication(hostContext.Configuration);

                services.AddSerilog((serviceProvider, loggerConfiguration) => loggerConfiguration
                    .ReadFrom.Configuration(hostContext.Configuration)
                    .ReadFrom.Services(serviceProvider)
                    .Enrich.FromLogContext()
                    .WriteTo.Console());
            });
    }
}
