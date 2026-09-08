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
            .SelectMany(reminder => CronSchedule.GetOccurrences(
                    reminder.CronExpression,
                    reminder.TimeZoneId,
                    reminder.CreatedAtUtc > from.UtcDateTime
                        ? AsUtcOffset(reminder.CreatedAtUtc)
                        : from,
                    to)
                .Select(occurrence => new ReminderOccurrenceDto(ToDto(reminder), occurrence)))
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
            .Where(x => x.NextOccurrenceUtc <= nowUtc)
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
        ValidateSchedule(request.CronExpression, request.TimeZoneId);

        var now = timeProvider.GetUtcNow();
        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            CronExpression = request.CronExpression.Trim(),
            TimeZoneId = request.TimeZoneId.Trim(),
            NextOccurrenceUtc = CronSchedule.GetNextOccurrence(
                request.CronExpression,
                request.TimeZoneId,
                now).UtcDateTime,
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
        ValidateSchedule(request.CronExpression, request.TimeZoneId);

        var reminder = await dbContext.Reminders
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (reminder is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        reminder.Title = request.Title.Trim();
        reminder.Message = request.Message.Trim();
        reminder.CronExpression = request.CronExpression.Trim();
        reminder.TimeZoneId = request.TimeZoneId.Trim();
        reminder.NextOccurrenceUtc = CronSchedule.GetNextOccurrence(
            request.CronExpression,
            request.TimeZoneId,
            now).UtcDateTime;
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
        reminder.NextOccurrenceUtc = CronSchedule.GetNextOccurrence(
            reminder.CronExpression,
            reminder.TimeZoneId,
            now).UtcDateTime;
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

    private static void ValidateSchedule(string cronExpression, string timeZoneId)
    {
        if (!CronSchedule.TryValidate(cronExpression, timeZoneId, out var error))
        {
            throw new InvalidCronScheduleException(error!);
        }
    }

    private static ReminderDto ToDto(Reminder reminder) => new(
        reminder.Id,
        reminder.Title,
        reminder.Message,
        reminder.CronExpression,
        reminder.TimeZoneId,
        AsUtcOffset(reminder.NextOccurrenceUtc),
        AsUtcOffset(reminder.CreatedAtUtc),
        AsUtcOffset(reminder.UpdatedAtUtc));

    private static DateTimeOffset AsUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
