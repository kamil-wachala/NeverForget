using NeverForget.Contracts;

namespace NeverForget.Server.Services;

public interface IGoogleCalendarService
{
    Task<IReadOnlyList<CalendarDto>> GetCalendarsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CalendarEventDto>> GetEventsAsync(
        IReadOnlyCollection<string> calendarIds,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);

    Task<CalendarEventDto?> GetEventAsync(
        string calendarId,
        string eventId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CalendarEventNotificationDto>> GetNotificationsAsync(
        IReadOnlyCollection<string> calendarIds,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);

    Task<CalendarEventDto> CreateEventAsync(
        CreateCalendarEventRequest request,
        CancellationToken cancellationToken);

    Task<CalendarEventDto?> UpdateEventAsync(
        string eventId,
        UpdateCalendarEventRequest request,
        CancellationToken cancellationToken);
}
