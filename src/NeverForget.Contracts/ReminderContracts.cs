using System.ComponentModel.DataAnnotations;

namespace NeverForget.Contracts;

public sealed record ReminderDto(
    Guid Id,
    string Title,
    string Message,
    DateTimeOffset ScheduledAt,
    bool IsAcknowledged,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateReminderRequest(
    [Required, StringLength(120)]
    string Title,
    [Required, StringLength(2000)]
    string Message,
    DateTimeOffset ScheduledAt);

public sealed record UpdateReminderRequest(
    [Required, StringLength(120)]
    string Title,
    [Required, StringLength(2000)]
    string Message,
    DateTimeOffset ScheduledAt);
