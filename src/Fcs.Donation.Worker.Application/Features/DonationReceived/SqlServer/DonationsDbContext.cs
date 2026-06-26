using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace Fcs.Donation.Worker.Application.Features.DonationReceived.SqlServer;

[ExcludeFromCodeCoverage]
public sealed class DonationsDbContext : DbContext
{
    public DonationsDbContext(DbContextOptions<DonationsDbContext> options) : base(options)
    {
    }

    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Donation>(builder =>
        {
            builder.ToTable("Donations", t => t.ExcludeFromMigrations());
            builder.HasKey(donation => donation.Id);
            builder.Property(donation => donation.CampaignId).IsRequired();
            builder.Property(donation => donation.DonorId).IsRequired();
            builder.Property(donation => donation.Amount).HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(donation => donation.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            builder.Property(donation => donation.CreatedAt).IsRequired();
            builder.Property(donation => donation.ProcessedAt);
            builder.Property(donation => donation.FailureReason).HasMaxLength(1000);
            builder.HasIndex(donation => donation.CampaignId);
            builder.HasIndex(donation => donation.DonorId);
            builder.HasIndex(donation => donation.Status);
            builder.HasIndex(donation => donation.CreatedAt);
        });

        modelBuilder.Entity<ProcessedMessage>(builder =>
        {
            builder.ToTable("ProcessedMessages");
            builder.HasKey(message => message.Id);
            builder.Property(message => message.MessageId).IsRequired();
            builder.Property(message => message.Topic).HasMaxLength(200).IsRequired();
            builder.Property(message => message.ProcessedAt).IsRequired();
            builder.HasIndex(message => new { message.MessageId, message.Topic }).IsUnique();
            builder.HasIndex(message => message.ProcessedAt);
        });
    }
}
