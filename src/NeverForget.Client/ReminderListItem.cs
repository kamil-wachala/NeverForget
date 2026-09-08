using NeverForget.Contracts;

namespace NeverForget.Client;

public sealed record ReminderListItem(ReminderDto Reminder)
{
    public Guid Id => Reminder.Id;
    public string Title => Reminder.Title;
    public string Message => Reminder.Message;
    public DateTime ScheduledAtLocal => Reminder.ScheduledAt.LocalDateTime;
    public string Status => Reminder.IsAcknowledged ? "Acknowledged" : "Pending";
}
