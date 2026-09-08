using Microsoft.EntityFrameworkCore;

namespace NeverForget.Server.Data;

public sealed class NeverForgetDbContext(DbContextOptions<NeverForgetDbContext> options)
    : DbContext(options)
{
    public DbSet<Reminder> Reminders => Set<Reminder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var reminder = modelBuilder.Entity<Reminder>();
        reminder.HasKey(x => x.Id);
        reminder.Property(x => x.Title).HasMaxLength(120).IsRequired();
        reminder.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        reminder.HasIndex(x => new { x.IsAcknowledged, x.ScheduledAtUtc });
    }
}
