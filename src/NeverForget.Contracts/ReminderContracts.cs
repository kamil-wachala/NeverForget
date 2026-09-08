using System.ComponentModel.DataAnnotations;

namespace NeverForget.Contracts;

public sealed record ReminderDto(
    Guid Id,
    string Title,
    string Message,
    string CronExpression,
    string TimeZoneId,
    DateTimeOffset NextOccurrence,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ReminderOccurrenceDto(
    ReminderDto Reminder,
    DateTimeOffset OccursAt);

public sealed record CreateReminderRequest(
    [Required, StringLength(120)]
    string Title,
    [Required, StringLength(2000)]
    string Message,
    [Required, StringLength(200)]
    string CronExpression,
    [Required, StringLength(100)]
    string TimeZoneId);

public sealed record UpdateReminderRequest(
    [Required, StringLength(120)]
    string Title,
    [Required, StringLength(2000)]
    string Message,
    [Required, StringLength(200)]
    string CronExpression,
    [Required, StringLength(100)]
    string TimeZoneId);
