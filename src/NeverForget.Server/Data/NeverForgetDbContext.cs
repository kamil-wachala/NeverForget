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
        reminder.Property(x => x.CronExpression).HasMaxLength(200).IsRequired();
        reminder.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        reminder.HasIndex(x => x.NextOccurrenceUtc);
    }
}
