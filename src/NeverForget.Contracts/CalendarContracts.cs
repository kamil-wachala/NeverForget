using System.ComponentModel.DataAnnotations;

namespace NeverForget.Contracts;

public sealed record CalendarDto(
    string Id,
    string Name,
    string? Description,
    string TimeZoneId,
    bool CanWrite,
    string? BackgroundColor);

public sealed record CalendarEventDto(
    string CalendarId,
    string CalendarName,
    string Id,
    string Title,
    string? Description,
    string? Location,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsAllDay,
    string TimeZoneId,
    int NotificationMinutesBefore,
    string? HtmlLink,
    DateTimeOffset? UpdatedAt);

public sealed record CalendarEventNotificationDto(
    CalendarEventDto Event,
    DateTimeOffset NotifyAt);

public sealed record CreateCalendarEventRequest(
    [Required, StringLength(1024)]
    string CalendarId,
    [Required, StringLength(120)]
    string Title,
    [StringLength(8000)]
    string? Description,
    [StringLength(500)]
    string? Location,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsAllDay,
    [Range(0, 40320)]
    int NotificationMinutesBefore);

public sealed record UpdateCalendarEventRequest(
    [Required, StringLength(1024)]
    string CalendarId,
    [Required, StringLength(120)]
    string Title,
    [StringLength(8000)]
    string? Description,
    [StringLength(500)]
    string? Location,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsAllDay,
    [Range(0, 40320)]
    int NotificationMinutesBefore);
