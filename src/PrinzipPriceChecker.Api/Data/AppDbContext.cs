using Microsoft.EntityFrameworkCore;
using PrinzipPriceChecker.Api.Domain;

namespace PrinzipPriceChecker.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TrackedFlat> TrackedFlats => Set<TrackedFlat>();

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public DbSet<PriceHistoryEntry> PriceHistory => Set<PriceHistoryEntry>();

    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TrackedFlat>(flat =>
        {
            flat.Property(f => f.Url).IsRequired().HasMaxLength(500);
            flat.HasIndex(f => f.Url).IsUnique();
            flat.Property(f => f.Name).HasMaxLength(500);
            flat.Property(f => f.Description).HasMaxLength(1000);
            flat.Property(f => f.LastCheckError).HasMaxLength(1000);
        });

        modelBuilder.Entity<Subscription>(subscription =>
        {
            // NOCASE: email нечувствителен к регистру
            subscription.Property(s => s.Email).IsRequired().HasMaxLength(320).UseCollation("NOCASE");
            subscription.HasOne(s => s.TrackedFlat)
                .WithMany(f => f.Subscriptions)
                .HasForeignKey(s => s.TrackedFlatId)
                .OnDelete(DeleteBehavior.Cascade);
            // Один и тот же email не должен подписываться на одну квартиру дважды.
            subscription.HasIndex(s => new { s.TrackedFlatId, s.Email }).IsUnique();
        });

        modelBuilder.Entity<PriceHistoryEntry>(change =>
        {
            change.HasOne(c => c.TrackedFlat)
                .WithMany(f => f.PriceHistory)
                .HasForeignKey(c => c.TrackedFlatId)
                .OnDelete(DeleteBehavior.Cascade);
            change.HasIndex(c => new { c.TrackedFlatId, c.DetectedAt });
        });

        modelBuilder.Entity<NotificationRecord>(notification =>
        {
            notification.Property(n => n.Email).IsRequired().HasMaxLength(320);
            notification.Property(n => n.FlatUrl).IsRequired().HasMaxLength(500);
            notification.Property(n => n.Subject).IsRequired().HasMaxLength(500);
            notification.Property(n => n.Error).HasMaxLength(2000);
            notification.HasIndex(n => n.CreatedAt);
        });
    }
}
