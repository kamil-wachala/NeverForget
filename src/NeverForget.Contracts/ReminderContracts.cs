using System.ComponentModel.DataAnnotations;

namespace NeverForget.Contracts;

public sealed record ReminderDto(
    Guid Id,
    string Title,
    string Message,
    bool IsRecurring,
    DateTimeOffset? ScheduledAt,
    string? CronExpression,
    string? TimeZoneId,
    DateTimeOffset? EndsAt,
    DateTimeOffset? NextOccurrence,
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
    bool IsRecurring,
    DateTimeOffset? ScheduledAt,
    [StringLength(200)]
    string? CronExpression,
    [StringLength(100)]
    string? TimeZoneId,
    DateTimeOffset? EndsAt);

public sealed record UpdateReminderRequest(
    [Required, StringLength(120)]
    string Title,
    [Required, StringLength(2000)]
    string Message,
    bool IsRecurring,
    DateTimeOffset? ScheduledAt,
    [StringLength(200)]
    string? CronExpression,
    [StringLength(100)]
    string? TimeZoneId,
    DateTimeOffset? EndsAt);
