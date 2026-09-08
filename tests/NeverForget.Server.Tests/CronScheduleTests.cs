using NeverForget.Scheduling;

namespace NeverForget.Server.Tests;

public sealed class CronScheduleTests
{
    [Theory]
    [InlineData("*/15 * * * *")]
    [InlineData("10 * * * *")]
    [InlineData("30 9 * * *")]
    [InlineData("0 8 * * 1-5")]
    [InlineData("0 12 1 * *")]
    public void Supported_cron_patterns_are_valid(string expression)
    {
        Assert.True(CronSchedule.TryValidate(expression, "UTC", out var error), error);
    }

    [Fact]
    public void Invalid_expression_is_rejected()
    {
        Assert.False(CronSchedule.TryValidate("not a schedule", "UTC", out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Weekday_schedule_skips_the_weekend()
    {
        var fridayMorning = new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);

        var next = CronSchedule.GetNextOccurrence("30 9 * * 1-5", "UTC", fridayMorning);

        Assert.Equal(new DateTimeOffset(2026, 9, 14, 9, 30, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void Occurrence_expansion_is_inclusive_and_limited()
    {
        var from = new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

        var occurrences = CronSchedule.GetOccurrences(
            "* * * * *",
            "UTC",
            from,
            from.AddHours(1),
            maximumCount: 5);

        Assert.Equal(5, occurrences.Count);
        Assert.Equal(from, occurrences[0]);
    }
}
