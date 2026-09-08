namespace NeverForget.Server.Services;

public sealed class InvalidCronScheduleException(string message) : Exception(message);
