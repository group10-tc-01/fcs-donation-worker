using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fcs.Donation.Worker.Application.Features.DonationReceived.SqlServer;

[ExcludeFromCodeCoverage]
internal sealed class DonationsDbContextFactory : IDesignTimeDbContextFactory<DonationsDbContext>
{
    public DonationsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DonationsDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost,1433;Database=DonationsDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True");
        return new DonationsDbContext(optionsBuilder.Options);
    }
}
