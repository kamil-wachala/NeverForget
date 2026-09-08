using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace NeverForget.Server.Data;

public static class DatabaseSchemaInitializer
{
    public static async Task InitializeAsync(
        NeverForgetDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        if (dbContext.Database.IsSqlite())
        {
            await UpgradeSqliteAsync(dbContext, cancellationToken);
        }
        else if (dbContext.Database.IsNpgsql())
        {
            await UpgradePostgresAsync(dbContext, cancellationToken);
        }
    }

    private static async Task UpgradeSqliteAsync(
        NeverForgetDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var columns = await GetSqliteColumnsAsync(dbContext, cancellationToken);
        var hadLegacySchedule = columns.Contains("ScheduledAtUtc");
        var addedCronExpression = !columns.Contains("CronExpression");

        if (addedCronExpression)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Reminders\" ADD COLUMN \"CronExpression\" TEXT NOT NULL DEFAULT '0 9 * * *';",
                cancellationToken);
        }

        if (!columns.Contains("TimeZoneId"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Reminders\" ADD COLUMN \"TimeZoneId\" TEXT NOT NULL DEFAULT 'UTC';",
                cancellationToken);
        }

        if (!columns.Contains("NextOccurrenceUtc"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Reminders\" ADD COLUMN \"NextOccurrenceUtc\" TEXT NOT NULL DEFAULT '0001-01-01 00:00:00';",
                cancellationToken);
        }

        if (!columns.Contains("IsRecurring"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Reminders\" ADD COLUMN \"IsRecurring\" INTEGER NOT NULL DEFAULT 1;",
                cancellationToken);
        }

        if (!columns.Contains("ScheduledAtUtc"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Reminders\" ADD COLUMN \"ScheduledAtUtc\" TEXT NULL;",
                cancellationToken);
        }

        if (!columns.Contains("EndsAtUtc"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Reminders\" ADD COLUMN \"EndsAtUtc\" TEXT NULL;",
                cancellationToken);
        }

        if (!columns.Contains("IsAcknowledged"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Reminders\" ADD COLUMN \"IsAcknowledged\" INTEGER NOT NULL DEFAULT 0;",
                cancellationToken);
        }

        if (addedCronExpression && hadLegacySchedule)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                UPDATE "Reminders"
                SET "CronExpression" = CAST(CAST(strftime('%M', "ScheduledAtUtc") AS INTEGER) AS TEXT)
                    || ' '
                    || CAST(CAST(strftime('%H', "ScheduledAtUtc") AS INTEGER) AS TEXT)
                    || ' * * *',
                    "TimeZoneId" = 'UTC',
                    "NextOccurrenceUtc" = "ScheduledAtUtc";
                """,
                cancellationToken);
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_Reminders_NextOccurrenceUtc\" ON \"Reminders\" (\"NextOccurrenceUtc\");",
            cancellationToken);
    }

    private static async Task<HashSet<string>> GetSqliteColumnsAsync(
        NeverForgetDbContext dbContext,
        CancellationToken cancellationToken)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(\"Reminders\");";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(1));
            }

            return columns;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task UpgradePostgresAsync(
        NeverForgetDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "Reminders" ADD COLUMN IF NOT EXISTS "CronExpression" character varying(200) NOT NULL DEFAULT '0 9 * * *';
            ALTER TABLE "Reminders" ADD COLUMN IF NOT EXISTS "TimeZoneId" character varying(100) NOT NULL DEFAULT 'UTC';
            ALTER TABLE "Reminders" ADD COLUMN IF NOT EXISTS "NextOccurrenceUtc" timestamp with time zone NOT NULL DEFAULT '-infinity';
            ALTER TABLE "Reminders" ADD COLUMN IF NOT EXISTS "IsRecurring" boolean NOT NULL DEFAULT TRUE;
            ALTER TABLE "Reminders" ADD COLUMN IF NOT EXISTS "ScheduledAtUtc" timestamp with time zone NULL;
            ALTER TABLE "Reminders" ADD COLUMN IF NOT EXISTS "EndsAtUtc" timestamp with time zone NULL;
            ALTER TABLE "Reminders" ADD COLUMN IF NOT EXISTS "IsAcknowledged" boolean NOT NULL DEFAULT FALSE;
            CREATE INDEX IF NOT EXISTS "IX_Reminders_NextOccurrenceUtc" ON "Reminders" ("NextOccurrenceUtc");
            """,
            cancellationToken);
    }
}
