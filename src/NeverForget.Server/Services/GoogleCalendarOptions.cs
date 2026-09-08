namespace NeverForget.Server.Services;

public sealed class GoogleCalendarOptions
{
    public const string SectionName = "GoogleCalendar";

    public string ServiceAccountCredentialPath { get; set; } = string.Empty;
    public string ServiceAccountCredentialJson { get; set; } = string.Empty;
    public string[] CalendarIds { get; set; } = [];
    public string[] ReadOnlyCalendarIds { get; set; } = [];
    public int DefaultNotificationMinutesBefore { get; set; } = 10;
}
