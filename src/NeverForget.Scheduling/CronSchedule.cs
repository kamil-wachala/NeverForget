using Cronos;

namespace NeverForget.Scheduling;

public static class CronSchedule
{
    public const int MaximumOccurrences = 1000;

    public static bool TryValidate(string? expression, string? timeZoneId, out string? error)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            error = "A cron expression is required.";
            return false;
        }

        try
        {
            _ = CronExpression.Parse(expression.Trim(), CronFormat.Standard);
        }
        catch (CronFormatException ex)
        {
            error = $"The cron expression is invalid: {ex.Message}";
            return false;
        }

        try
        {
            _ = ResolveTimeZone(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            error = $"The time zone '{timeZoneId}' is not available on the server.";
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            error = $"The time zone '{timeZoneId}' is invalid.";
            return false;
        }

        error = null;
        return true;
    }

    public static DateTimeOffset GetNextOccurrence(
        string expression,
        string timeZoneId,
        DateTimeOffset after,
        bool inclusive = false)
    {
        var cron = CronExpression.Parse(expression.Trim(), CronFormat.Standard);
        var timeZone = ResolveTimeZone(timeZoneId);
        return cron.GetNextOccurrence(after, timeZone, inclusive)
            ?? throw new InvalidOperationException("The cron expression has no future occurrence.");
    }

    public static IReadOnlyList<DateTimeOffset> GetOccurrences(
        string expression,
        string timeZoneId,
        DateTimeOffset from,
        DateTimeOffset to,
        int maximumCount = MaximumOccurrences)
    {
        if (maximumCount <= 0)
        {
            return [];
        }

        var cron = CronExpression.Parse(expression.Trim(), CronFormat.Standard);
        var timeZone = ResolveTimeZone(timeZoneId);

        return cron.GetOccurrences(from, to, timeZone, fromInclusive: true, toInclusive: true)
            .Take(maximumCount)
            .ToList();
    }

    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new TimeZoneNotFoundException("A time zone is required.");
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        }
        catch (TimeZoneNotFoundException)
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId.Trim(), out var windowsId))
            {
                return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
            }

            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId.Trim(), out var ianaId))
            {
                return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
            }

            throw;
        }
    }

    public static string GetPortableTimeZoneId(TimeZoneInfo timeZone)
    {
        if (timeZone.Equals(TimeZoneInfo.Utc))
        {
            return "UTC";
        }

        return TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZone.Id, out var ianaId)
            ? ianaId
            : timeZone.Id;
    }
}
