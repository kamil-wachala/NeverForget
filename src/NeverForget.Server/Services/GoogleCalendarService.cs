using System.Globalization;
using System.Net;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Options;
using NeverForget.Contracts;
using GoogleCalendar = Google.Apis.Calendar.v3.Data.Calendar;
using GoogleEvent = Google.Apis.Calendar.v3.Data.Event;

namespace NeverForget.Server.Services;

public sealed class GoogleCalendarService : IGoogleCalendarService, IDisposable
{
    private const int MaximumEventsPerCalendar = 2500;
    private readonly GoogleCalendarOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly Lazy<CalendarService> _calendarService;
    private readonly HashSet<string> _readOnlyCalendarIds;

    public GoogleCalendarService(
        IOptions<GoogleCalendarOptions> options,
        IWebHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
        _readOnlyCalendarIds = _options.ReadOnlyCalendarIds
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _calendarService = new Lazy<CalendarService>(CreateCalendarService);
    }

    public async Task<IReadOnlyList<CalendarDto>> GetCalendarsAsync(CancellationToken cancellationToken)
    {
        EnsureCalendarIdsConfigured();
        var calendars = new List<CalendarDto>();
        foreach (var calendarId in GetConfiguredCalendarIds())
        {
            var calendar = await _calendarService.Value.Calendars
                .Get(calendarId)
                .ExecuteAsync(cancellationToken);
            calendars.Add(ToDto(calendarId, calendar));
        }

        return calendars.OrderBy(x => x.Name).ToList();
    }

    public async Task<IReadOnlyList<CalendarEventDto>> GetEventsAsync(
        IReadOnlyCollection<string> calendarIds,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        ValidateRange(from, to);
        var calendars = await ResolveCalendarsAsync(calendarIds, cancellationToken);
        var result = new List<CalendarEventDto>();

        foreach (var calendar in calendars)
        {
            var request = _calendarService.Value.Events.List(calendar.Id);
            request.TimeMinDateTimeOffset = from;
            request.TimeMaxDateTimeOffset = to;
            request.SingleEvents = true;
            request.ShowDeleted = false;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
            request.MaxResults = MaximumEventsPerCalendar;

            string? pageToken = null;
            do
            {
                request.PageToken = pageToken;
                var page = await request.ExecuteAsync(cancellationToken);
                result.AddRange((page.Items ?? [])
                    .Where(x => !string.Equals(x.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
                    .Select(x => ToDto(calendar, x)));
                pageToken = page.NextPageToken;
            } while (!string.IsNullOrWhiteSpace(pageToken));
        }

        return result
            .OrderBy(x => x.StartsAt)
            .ThenBy(x => x.CalendarName)
            .ToList();
    }

    public async Task<CalendarEventDto?> GetEventAsync(
        string calendarId,
        string eventId,
        CancellationToken cancellationToken)
    {
        var calendar = await ResolveCalendarAsync(calendarId, cancellationToken);
        try
        {
            var googleEvent = await _calendarService.Value.Events
                .Get(calendar.Id, eventId)
                .ExecuteAsync(cancellationToken);
            return ToDto(calendar, googleEvent);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<CalendarEventNotificationDto>> GetNotificationsAsync(
        IReadOnlyCollection<string> calendarIds,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        ValidateRange(from, to);
        var events = await GetEventsAsync(
            calendarIds,
            from,
            to.AddMinutes(40320),
            cancellationToken);

        return events
            .Select(x => new CalendarEventNotificationDto(
                x,
                x.StartsAt.AddMinutes(-x.NotificationMinutesBefore)))
            .Where(x => x.NotifyAt >= from && x.NotifyAt <= to)
            .OrderBy(x => x.NotifyAt)
            .ToList();
    }

    public async Task<CalendarEventDto> CreateEventAsync(
        CreateCalendarEventRequest request,
        CancellationToken cancellationToken)
    {
        ValidateEvent(request.Title, request.StartsAt, request.EndsAt, request.IsAllDay);
        var calendar = await ResolveWritableCalendarAsync(request.CalendarId, cancellationToken);
        var googleEvent = BuildGoogleEvent(
            new GoogleEvent(),
            request.Title,
            request.Description,
            request.Location,
            request.StartsAt,
            request.EndsAt,
            request.IsAllDay,
            calendar.TimeZoneId,
            request.NotificationMinutesBefore);

        var created = await _calendarService.Value.Events
            .Insert(googleEvent, calendar.Id)
            .ExecuteAsync(cancellationToken);
        return ToDto(calendar, created);
    }

    public async Task<CalendarEventDto?> UpdateEventAsync(
        string eventId,
        UpdateCalendarEventRequest request,
        CancellationToken cancellationToken)
    {
        ValidateEvent(request.Title, request.StartsAt, request.EndsAt, request.IsAllDay);
        var calendar = await ResolveWritableCalendarAsync(request.CalendarId, cancellationToken);

        GoogleEvent existing;
        try
        {
            existing = await _calendarService.Value.Events
                .Get(calendar.Id, eventId)
                .ExecuteAsync(cancellationToken);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        BuildGoogleEvent(
            existing,
            request.Title,
            request.Description,
            request.Location,
            request.StartsAt,
            request.EndsAt,
            request.IsAllDay,
            calendar.TimeZoneId,
            request.NotificationMinutesBefore);

        var updated = await _calendarService.Value.Events
            .Update(existing, calendar.Id, eventId)
            .ExecuteAsync(cancellationToken);
        return ToDto(calendar, updated);
    }

    private async Task<IReadOnlyList<CalendarDto>> ResolveCalendarsAsync(
        IReadOnlyCollection<string> calendarIds,
        CancellationToken cancellationToken)
    {
        var requestedIds = calendarIds.Count == 0
            ? GetConfiguredCalendarIds()
            : calendarIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var calendars = new List<CalendarDto>();
        foreach (var id in requestedIds)
        {
            calendars.Add(await ResolveCalendarAsync(id, cancellationToken));
        }

        return calendars;
    }

    private async Task<CalendarDto> ResolveCalendarAsync(
        string calendarId,
        CancellationToken cancellationToken)
    {
        EnsureConfiguredCalendar(calendarId);
        var calendar = await _calendarService.Value.Calendars
            .Get(calendarId)
            .ExecuteAsync(cancellationToken);
        return ToDto(calendarId, calendar);
    }

    private async Task<CalendarDto> ResolveWritableCalendarAsync(
        string calendarId,
        CancellationToken cancellationToken)
    {
        var calendar = await ResolveCalendarAsync(calendarId, cancellationToken);
        if (!calendar.CanWrite)
        {
            throw new CalendarEventValidationException(
                $"Calendar '{calendar.Name}' is configured as read-only.");
        }

        return calendar;
    }

    private CalendarDto ToDto(string calendarId, GoogleCalendar calendar) => new(
        calendarId,
        string.IsNullOrWhiteSpace(calendar.Summary) ? calendarId : calendar.Summary,
        calendar.Description,
        string.IsNullOrWhiteSpace(calendar.TimeZone) ? "UTC" : calendar.TimeZone,
        !_readOnlyCalendarIds.Contains(calendarId),
        null);

    private CalendarEventDto ToDto(CalendarDto calendar, GoogleEvent googleEvent)
    {
        var (startsAt, isAllDay) = ReadEventDateTime(googleEvent.Start, calendar.TimeZoneId);
        var (endsAt, _) = ReadEventDateTime(googleEvent.End, calendar.TimeZoneId);
        var notificationMinutes = googleEvent.Reminders?.Overrides?
            .FirstOrDefault(x => string.Equals(x.Method, "popup", StringComparison.OrdinalIgnoreCase))
            ?.Minutes
            ?? Math.Clamp(_options.DefaultNotificationMinutesBefore, 0, 40320);

        return new CalendarEventDto(
            calendar.Id,
            calendar.Name,
            googleEvent.Id,
            string.IsNullOrWhiteSpace(googleEvent.Summary) ? "(Untitled event)" : googleEvent.Summary,
            googleEvent.Description,
            googleEvent.Location,
            startsAt,
            endsAt,
            isAllDay,
            googleEvent.Start?.TimeZone ?? calendar.TimeZoneId,
            notificationMinutes,
            googleEvent.HtmlLink,
            googleEvent.UpdatedDateTimeOffset);
    }

    private static GoogleEvent BuildGoogleEvent(
        GoogleEvent googleEvent,
        string title,
        string? description,
        string? location,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        bool isAllDay,
        string timeZoneId,
        int notificationMinutesBefore)
    {
        googleEvent.Summary = title.Trim();
        googleEvent.Description = NullIfWhiteSpace(description);
        googleEvent.Location = NullIfWhiteSpace(location);
        if (isAllDay)
        {
            googleEvent.Start = new EventDateTime { Date = startsAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
            googleEvent.End = new EventDateTime { Date = endsAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
        }
        else
        {
            googleEvent.Start = new EventDateTime
            {
                DateTimeDateTimeOffset = startsAt,
                TimeZone = timeZoneId
            };
            googleEvent.End = new EventDateTime
            {
                DateTimeDateTimeOffset = endsAt,
                TimeZone = timeZoneId
            };
        }

        googleEvent.Reminders = new GoogleEvent.RemindersData
        {
            UseDefault = false,
            Overrides =
            [
                new EventReminder
                {
                    Method = "popup",
                    Minutes = notificationMinutesBefore
                }
            ]
        };
        return googleEvent;
    }

    private CalendarService CreateCalendarService()
    {
        GoogleCredential credential;
        if (!string.IsNullOrWhiteSpace(_options.ServiceAccountCredentialJson))
        {
            credential = CredentialFactory
                .FromJson<ServiceAccountCredential>(_options.ServiceAccountCredentialJson)
                .ToGoogleCredential();
        }
        else if (!string.IsNullOrWhiteSpace(_options.ServiceAccountCredentialPath))
        {
            var path = Path.IsPathRooted(_options.ServiceAccountCredentialPath)
                ? _options.ServiceAccountCredentialPath
                : Path.Combine(_environment.ContentRootPath, _options.ServiceAccountCredentialPath);
            if (!File.Exists(path))
            {
                throw new GoogleCalendarConfigurationException(
                    $"The Google service-account credential file was not found at '{path}'.");
            }

            credential = CredentialFactory
                .FromFile<ServiceAccountCredential>(path)
                .ToGoogleCredential();
        }
        else
        {
            throw new GoogleCalendarConfigurationException(
                "Set GoogleCalendar:ServiceAccountCredentialPath or GoogleCalendar:ServiceAccountCredentialJson.");
        }

        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential.CreateScoped(CalendarService.Scope.Calendar),
            ApplicationName = "NeverForget"
        });
    }

    private string[] GetConfiguredCalendarIds() =>
        _options.CalendarIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private void EnsureCalendarIdsConfigured()
    {
        if (GetConfiguredCalendarIds().Length == 0)
        {
            throw new GoogleCalendarConfigurationException(
                "Add at least one calendar ID to GoogleCalendar:CalendarIds.");
        }
    }

    private void EnsureConfiguredCalendar(string calendarId)
    {
        if (string.IsNullOrWhiteSpace(calendarId)
            || !GetConfiguredCalendarIds().Contains(calendarId, StringComparer.OrdinalIgnoreCase))
        {
            throw new CalendarEventValidationException("Select a configured Google calendar.");
        }
    }

    private static void ValidateRange(DateTimeOffset from, DateTimeOffset to)
    {
        if (from > to)
        {
            throw new CalendarEventValidationException(
                "The start date cannot be later than the end date.");
        }
    }

    private static void ValidateEvent(
        string title,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        bool isAllDay)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new CalendarEventValidationException("The event title is required.");
        }

        if (isAllDay ? endsAt.Date <= startsAt.Date : endsAt <= startsAt)
        {
            throw new CalendarEventValidationException(
                isAllDay
                    ? "An all-day event must end on a later date."
                    : "The event end time must be later than its start time.");
        }
    }

    private static (DateTimeOffset Value, bool IsAllDay) ReadEventDateTime(
        EventDateTime? value,
        string fallbackTimeZoneId)
    {
        if (value?.DateTimeDateTimeOffset is DateTimeOffset dateTime)
        {
            return (dateTime, false);
        }

        if (value is null || !DateOnly.TryParseExact(
                value.Date,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            throw new InvalidOperationException("Google Calendar returned an event without a valid date or time.");
        }

        var localMidnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var timeZone = ResolveTimeZone(value.TimeZone ?? fallbackTimeZoneId);
        return (new DateTimeOffset(localMidnight, timeZone.GetUtcOffset(localMidnight)), true);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windowsId))
            {
                return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
            }

            throw;
        }
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public void Dispose()
    {
        if (_calendarService.IsValueCreated)
        {
            _calendarService.Value.Dispose();
        }
    }
}
