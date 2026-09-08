namespace NeverForget.Server.Services;

public sealed class CalendarEventValidationException(string message) : Exception(message);

public sealed class GoogleCalendarConfigurationException(string message) : Exception(message);
