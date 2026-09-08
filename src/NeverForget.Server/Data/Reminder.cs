namespace NeverForget.Server.Data;

public sealed class Reminder
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRecurring { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "UTC";
    public DateTime? EndsAtUtc { get; set; }
    public DateTime NextOccurrenceUtc { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
