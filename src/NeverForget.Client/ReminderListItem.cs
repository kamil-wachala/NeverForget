using NeverForget.Contracts;

namespace NeverForget.Client;

public sealed record ReminderListItem(ReminderOccurrenceDto Occurrence)
{
    public ReminderDto Reminder => Occurrence.Reminder;
    public Guid Id => Reminder.Id;
    public string Title => Reminder.Title;
    public string Message => Reminder.Message;
    public bool IsRecurring => Reminder.IsRecurring;
    public DateTimeOffset? ScheduledAt => Reminder.ScheduledAt;
    public string? CronExpression => Reminder.CronExpression;
    public string? TimeZoneId => Reminder.TimeZoneId;
    public DateTimeOffset? EndsAt => Reminder.EndsAt;
    public string Schedule => Reminder.IsRecurring ? Reminder.CronExpression ?? string.Empty : "One time";
    public DateTime OccursAtLocal => Occurrence.OccursAt.LocalDateTime;
}
