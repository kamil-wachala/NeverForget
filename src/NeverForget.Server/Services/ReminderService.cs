using Microsoft.EntityFrameworkCore;
using NeverForget.Contracts;
using NeverForget.Server.Data;

namespace NeverForget.Server.Services;

public sealed class ReminderService(
    NeverForgetDbContext dbContext,
    TimeProvider timeProvider) : IReminderService
{
    public async Task<IReadOnlyList<ReminderDto>> GetBetweenAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var fromUtc = from.UtcDateTime;
        var toUtc = to.UtcDateTime;

        return await dbContext.Reminders
            .AsNoTracking()
            .Where(x => x.ScheduledAtUtc >= fromUtc && x.ScheduledAtUtc <= toUtc)
            .OrderBy(x => x.ScheduledAtUtc)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReminderDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var reminder = await dbContext.Reminders
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return reminder is null ? null : ToDto(reminder);
    }

    public async Task<IReadOnlyList<ReminderDto>> GetDueAsync(CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        return await dbContext.Reminders
            .AsNoTracking()
            .Where(x => !x.IsAcknowledged && x.ScheduledAtUtc <= nowUtc)
            .OrderBy(x => x.ScheduledAtUtc)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReminderDto> CreateAsync(
        CreateReminderRequest request,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            ScheduledAtUtc = request.ScheduledAt.UtcDateTime,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
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

        reminder.Title = request.Title.Trim();
        reminder.Message = request.Message.Trim();
        reminder.ScheduledAtUtc = request.ScheduledAt.UtcDateTime;
        reminder.IsAcknowledged = false;
        reminder.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
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

        reminder.IsAcknowledged = true;
        reminder.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
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

    private static ReminderDto ToDto(Reminder reminder) => new(
        reminder.Id,
        reminder.Title,
        reminder.Message,
        new DateTimeOffset(DateTime.SpecifyKind(reminder.ScheduledAtUtc, DateTimeKind.Utc)),
        reminder.IsAcknowledged,
        new DateTimeOffset(DateTime.SpecifyKind(reminder.CreatedAtUtc, DateTimeKind.Utc)),
        new DateTimeOffset(DateTime.SpecifyKind(reminder.UpdatedAtUtc, DateTimeKind.Utc)));
}
