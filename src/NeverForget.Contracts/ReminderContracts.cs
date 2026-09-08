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
    string Title,
    string Message,
    DateTimeOffset ScheduledAt);

public sealed record UpdateReminderRequest(
    string Title,
    string Message,
    DateTimeOffset ScheduledAt);
