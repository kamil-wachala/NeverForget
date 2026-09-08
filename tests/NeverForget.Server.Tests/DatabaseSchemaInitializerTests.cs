using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NeverForget.Server.Data;

namespace NeverForget.Server.Tests;

public sealed class DatabaseSchemaInitializerTests
{
    [Fact]
    public async Task Legacy_sqlite_reminder_is_upgraded_without_deleting_it()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                CREATE TABLE "Reminders" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Reminders" PRIMARY KEY,
                    "Title" TEXT NOT NULL,
                    "Message" TEXT NOT NULL,
                    "ScheduledAtUtc" TEXT NOT NULL,
                    "IsAcknowledged" INTEGER NOT NULL,
                    "CreatedAtUtc" TEXT NOT NULL,
                    "UpdatedAtUtc" TEXT NOT NULL
                );
                INSERT INTO "Reminders"
                    ("Id", "Title", "Message", "ScheduledAtUtc", "IsAcknowledged", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES
                    ('11111111-1111-1111-1111-111111111111', 'Legacy', 'Keep me',
                     '2026-09-08 18:30:00', 0, '2026-09-01 10:00:00', '2026-09-01 10:00:00');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<NeverForgetDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new NeverForgetDbContext(options);

        await DatabaseSchemaInitializer.InitializeAsync(dbContext);
        var reminder = await dbContext.Reminders.SingleAsync();

        Assert.Equal("Legacy", reminder.Title);
        Assert.Equal("30 18 * * *", reminder.CronExpression);
        Assert.Equal("UTC", reminder.TimeZoneId);
        Assert.Equal(new DateTime(2026, 9, 8, 18, 30, 0), reminder.NextOccurrenceUtc);
    }
}
