using NeverForget.Contracts;

namespace NeverForget.Client;

public sealed record CalendarEventListItem(CalendarEventDto Event)
{
    public string Id => Event.Id;
    public string CalendarId => Event.CalendarId;
    public string CalendarName => Event.CalendarName;
    public string Title => Event.Title;
    public DateTime StartsAtLocal => Event.StartsAt.LocalDateTime;
    public DateTime EndsAtLocal => Event.EndsAt.LocalDateTime;
    public string Notification => $"{Event.NotificationMinutesBefore} min before";
}
