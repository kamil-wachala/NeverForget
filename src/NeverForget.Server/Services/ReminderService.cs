using Microsoft.EntityFrameworkCore;
using NeverForget.Contracts;
using NeverForget.Scheduling;
using NeverForget.Server.Data;

namespace NeverForget.Server.Services;

public sealed class ReminderService(
    NeverForgetDbContext dbContext,
    TimeProvider timeProvider) : IReminderService
{
    public async Task<IReadOnlyList<ReminderOccurrenceDto>> GetBetweenAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var reminders = await dbContext.Reminders
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return reminders
            .SelectMany(reminder => GetOccurrences(reminder, from, to))
            .OrderBy(x => x.OccursAt)
            .Take(CronSchedule.MaximumOccurrences)
            .ToList();
    }

    public async Task<ReminderDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var reminder = await dbContext.Reminders
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return reminder is null ? null : ToDto(reminder);
    }

    public async Task<IReadOnlyList<ReminderOccurrenceDto>> GetDueAsync(CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var reminders = await dbContext.Reminders
            .AsNoTracking()
            .Where(x => !x.IsAcknowledged && x.NextOccurrenceUtc <= nowUtc)
            .OrderBy(x => x.NextOccurrenceUtc)
            .ToListAsync(cancellationToken);

        return reminders
            .Select(x => new ReminderOccurrenceDto(ToDto(x), AsUtcOffset(x.NextOccurrenceUtc)))
            .ToList();
    }

    public async Task<ReminderDto> CreateAsync(
        CreateReminderRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var schedule = ValidateAndBuildSchedule(
            request.IsRecurring,
            request.ScheduledAt,
            request.CronExpression,
            request.TimeZoneId,
            request.EndsAt,
            now);

        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            IsRecurring = request.IsRecurring,
            ScheduledAtUtc = schedule.ScheduledAtUtc,
            CronExpression = schedule.CronExpression,
            TimeZoneId = schedule.TimeZoneId,
            EndsAtUtc = schedule.EndsAtUtc,
            NextOccurrenceUtc = schedule.NextOccurrenceUtc,
            IsAcknowledged = false,
            CreatedAtUtc = now.UtcDateTime,
            UpdatedAtUtc = now.UtcDateTime
        };

        dbContext.Reminders.Add(reminder);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(reminder);
    }

    public async Task<ReminderDto?> UpdateAsync(
        Guid id,
        UpdateReminderRequest request,
        CancellationToken cancellationToken)
    {
        var reminder = await dbContext.Reminders
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (reminder is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var schedule = ValidateAndBuildSchedule(
            request.IsRecurring,
            request.ScheduledAt,
            request.CronExpression,
            request.TimeZoneId,
            request.EndsAt,
            now);

        reminder.Title = request.Title.Trim();
        reminder.Message = request.Message.Trim();
        reminder.IsRecurring = request.IsRecurring;
        reminder.ScheduledAtUtc = schedule.ScheduledAtUtc;
        reminder.CronExpression = schedule.CronExpression;
        reminder.TimeZoneId = schedule.TimeZoneId;
        reminder.EndsAtUtc = schedule.EndsAtUtc;
        reminder.NextOccurrenceUtc = schedule.NextOccurrenceUtc;
        reminder.IsAcknowledged = false;
        reminder.UpdatedAtUtc = now.UtcDateTime;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(reminder);
    }

    public async Task<bool> AcknowledgeAsync(Guid id, CancellationToken cancellationToken)
    {
        var reminder = await dbContext.Reminders
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (reminder is null)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        if (!reminder.IsRecurring)
        {
            reminder.IsAcknowledged = true;
        }
        else
        {
            var next = CronSchedule.GetNextOccurrence(
                reminder.CronExpression,
                reminder.TimeZoneId,
                now);
            if (reminder.EndsAtUtc is DateTime endsAtUtc && next.UtcDateTime > endsAtUtc)
            {
                reminder.IsAcknowledged = true;
            }
            else
            {
                reminder.NextOccurrenceUtc = next.UtcDateTime;
            }
        }

        reminder.UpdatedAtUtc = now.UtcDateTime;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var reminder = await dbContext.Reminders
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (reminder is null)
        {
            return false;
        }

        dbContext.Reminders.Remove(reminder);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static IEnumerable<ReminderOccurrenceDto> GetOccurrences(
        Reminder reminder,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        if (!reminder.IsRecurring)
        {
            if (reminder.ScheduledAtUtc is DateTime scheduledAtUtc)
            {
                var scheduledAt = AsUtcOffset(scheduledAtUtc);
                if (scheduledAt >= from && scheduledAt <= to)
                {
                    yield return new ReminderOccurrenceDto(ToDto(reminder), scheduledAt);
                }
            }

            yield break;
        }

        var effectiveFrom = reminder.CreatedAtUtc > from.UtcDateTime
            ? AsUtcOffset(reminder.CreatedAtUtc)
            : from;
        var effectiveTo = reminder.EndsAtUtc is DateTime endsAtUtc && endsAtUtc < to.UtcDateTime
            ? AsUtcOffset(endsAtUtc)
            : to;
        if (effectiveFrom > effectiveTo)
        {
            yield break;
        }

        foreach (var occurrence in CronSchedule.GetOccurrences(
                     reminder.CronExpression,
                     reminder.TimeZoneId,
                     effectiveFrom,
                     effectiveTo))
        {
            yield return new ReminderOccurrenceDto(ToDto(reminder), occurrence);
        }
    }

    private static ScheduleValues ValidateAndBuildSchedule(
        bool isRecurring,
        DateTimeOffset? scheduledAt,
        string? cronExpression,
        string? timeZoneId,
        DateTimeOffset? endsAt,
        DateTimeOffset now)
    {
        if (!isRecurring)
        {
            if (scheduledAt is null)
            {
                throw new InvalidCronScheduleException("A date and time are required for a one-time reminder.");
            }

            if (scheduledAt <= now)
            {
                throw new InvalidCronScheduleException("The reminder date and time must be in the future.");
            }

            return new ScheduleValues(
                scheduledAt.Value.UtcDateTime,
                string.Empty,
                "UTC",
                null,
                scheduledAt.Value.UtcDateTime);
        }

        if (!CronSchedule.TryValidate(cronExpression, timeZoneId, out var error))
        {
            throw new InvalidCronScheduleException(error!);
        }

        if (endsAt is not null && endsAt <= now)
        {
            throw new InvalidCronScheduleException("The recurrence end date must be in the future.");
        }

        var normalizedCron = cronExpression!.Trim();
        var normalizedTimeZone = timeZoneId!.Trim();
        var next = CronSchedule.GetNextOccurrence(normalizedCron, normalizedTimeZone, now);
        if (endsAt is not null && next > endsAt)
        {
            throw new InvalidCronScheduleException("The schedule has no occurrence on or before its end date.");
        }

        return new ScheduleValues(
            null,
            normalizedCron,
            normalizedTimeZone,
            endsAt?.UtcDateTime,
            next.UtcDateTime);
    }

    private static ReminderDto ToDto(Reminder reminder) => new(
        reminder.Id,
        reminder.Title,
        reminder.Message,
        reminder.IsRecurring,
        reminder.ScheduledAtUtc is DateTime scheduledAtUtc ? AsUtcOffset(scheduledAtUtc) : null,
        reminder.IsRecurring ? reminder.CronExpression : null,
        reminder.IsRecurring ? reminder.TimeZoneId : null,
        reminder.EndsAtUtc is DateTime endsAtUtc ? AsUtcOffset(endsAtUtc) : null,
        reminder.IsAcknowledged ? null : AsUtcOffset(reminder.NextOccurrenceUtc),
        AsUtcOffset(reminder.CreatedAtUtc),
        AsUtcOffset(reminder.UpdatedAtUtc));

    private static DateTimeOffset AsUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record ScheduleValues(
        DateTime? ScheduledAtUtc,
        string CronExpression,
        string TimeZoneId,
        DateTime? EndsAtUtc,
        DateTime NextOccurrenceUtc);
}
