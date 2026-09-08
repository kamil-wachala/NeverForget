using NeverForget.Contracts;

namespace NeverForget.Client;

public sealed record ReminderListItem(ReminderOccurrenceDto Occurrence)
{
    public ReminderDto Reminder => Occurrence.Reminder;
    public Guid Id => Reminder.Id;
    public string Title => Reminder.Title;
    public string Message => Reminder.Message;
    public string CronExpression => Reminder.CronExpression;
    public string TimeZoneId => Reminder.TimeZoneId;
    public DateTime OccursAtLocal => Occurrence.OccursAt.LocalDateTime;
}
