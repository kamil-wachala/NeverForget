using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NeverForget.Contracts;
using NeverForget.Server.Services;

namespace NeverForget.Server.Tests;

public sealed class CalendarApiTests : IClassFixture<CalendarApiFactory>
{
    private readonly CalendarApiFactory _factory;
    private readonly HttpClient _client;

    public CalendarApiTests(CalendarApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task Configured_calendars_are_returned()
    {
        var calendars = await _client.GetFromJsonAsync<List<CalendarDto>>("/api/calendars");

        Assert.Equal(2, calendars!.Count);
        Assert.Contains(calendars, x => x.Id == "work@example.com" && x.CanWrite);
        Assert.Contains(calendars, x => x.Id == "holidays@example.com" && !x.CanWrite);
    }

    [Fact]
    public async Task Events_can_be_filtered_by_calendar_and_date_range()
    {
        var from = Uri.EscapeDataString("2026-09-08T00:00:00Z");
        var to = Uri.EscapeDataString("2026-09-09T00:00:00Z");
        var calendar = Uri.EscapeDataString("work@example.com");

        var events = await _client.GetFromJsonAsync<List<CalendarEventDto>>(
            $"/api/calendar-events?from={from}&to={to}&calendarIds={calendar}");

        var calendarEvent = Assert.Single(events!);
        Assert.Equal("Planning", calendarEvent.Title);
        Assert.Equal("work@example.com", calendarEvent.CalendarId);
    }

    [Fact]
    public async Task Event_can_be_created_and_updated()
    {
        var create = new CreateCalendarEventRequest(
            "work@example.com",
            "Architecture review",
            "Review the calendar pivot",
            "Meeting room",
            new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 10, 10, 0, 0, TimeSpan.Zero),
            false,
            15);

        var createResponse = await _client.PostAsJsonAsync("/api/calendar-events", create);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = (await createResponse.Content.ReadFromJsonAsync<CalendarEventDto>())!;
        Assert.Equal("Architecture review", created.Title);
        Assert.Equal(15, created.NotificationMinutesBefore);

        var update = new UpdateCalendarEventRequest(
            created.CalendarId,
            "Updated architecture review",
            created.Description,
            "Online",
            created.StartsAt,
            created.EndsAt.AddMinutes(30),
            false,
            5);
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/calendar-events/{created.Id}",
            update);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = (await updateResponse.Content.ReadFromJsonAsync<CalendarEventDto>())!;
        Assert.Equal("Updated architecture review", updated.Title);
        Assert.Equal("Online", updated.Location);
        Assert.Equal(5, updated.NotificationMinutesBefore);
    }

    [Fact]
    public async Task Notification_endpoint_returns_events_at_their_lead_time()
    {
        var from = Uri.EscapeDataString("2026-09-08T08:49:00Z");
        var to = Uri.EscapeDataString("2026-09-08T08:51:00Z");
        var calendar = Uri.EscapeDataString("work@example.com");

        var notifications = await _client.GetFromJsonAsync<List<CalendarEventNotificationDto>>(
            $"/api/calendar-events/notifications?from={from}&to={to}&calendarIds={calendar}");

        var notification = Assert.Single(notifications!);
        Assert.Equal(new DateTimeOffset(2026, 9, 8, 8, 50, 0, TimeSpan.Zero), notification.NotifyAt);
        Assert.Equal("Planning", notification.Event.Title);
    }

    [Fact]
    public async Task Invalid_range_and_invalid_event_return_bad_request()
    {
        var invalidRange = await _client.GetAsync(
            "/api/calendar-events?from=2026-09-09T00%3A00%3A00Z&to=2026-09-08T00%3A00%3A00Z");
        var invalidEvent = await _client.PostAsJsonAsync(
            "/api/calendar-events",
            new CreateCalendarEventRequest(
                "work@example.com",
                " ",
                null,
                null,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddHours(1),
                false,
                10));

        Assert.Equal(HttpStatusCode.BadRequest, invalidRange.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidEvent.StatusCode);
    }

    [Fact]
    public async Task Missing_google_configuration_returns_service_unavailable()
    {
        await using var unconfiguredFactory = new WebApplicationFactory<Program>();
        using var client = unconfiguredFactory.CreateClient();

        var response = await client.GetAsync("/api/calendars");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}

public sealed class CalendarApiFactory : WebApplicationFactory<Program>
{
    public FakeGoogleCalendarService CalendarService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGoogleCalendarService>();
            services.AddSingleton<IGoogleCalendarService>(CalendarService);
        });
    }
}

public sealed class FakeGoogleCalendarService : IGoogleCalendarService
{
    private readonly object _gate = new();
    private readonly List<CalendarDto> _calendars =
    [
        new("work@example.com", "Work", null, "UTC", true, "#3157D5"),
        new("holidays@example.com", "Holidays", null, "UTC", false, "#2E7D32")
    ];

    private readonly List<CalendarEventDto> _events =
    [
        new(
            "work@example.com",
            "Work",
            "planning-1",
            "Planning",
            "Weekly planning",
            null,
            new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 8, 10, 0, 0, TimeSpan.Zero),
            false,
            "UTC",
            10,
            "https://calendar.google.com/event?eid=planning-1",
            new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)),
        new(
            "holidays@example.com",
            "Holidays",
            "holiday-1",
            "Holiday",
            null,
            null,
            new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 13, 0, 0, 0, TimeSpan.Zero),
            true,
            "UTC",
            0,
            null,
            null)
    ];

    public Task<IReadOnlyList<CalendarDto>> GetCalendarsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CalendarDto>>(_calendars);

    public Task<IReadOnlyList<CalendarEventDto>> GetEventsAsync(
        IReadOnlyCollection<string> calendarIds,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var ids = calendarIds.Count == 0
                ? _calendars.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : calendarIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult<IReadOnlyList<CalendarEventDto>>(_events
                .Where(x => ids.Contains(x.CalendarId) && x.EndsAt > from && x.StartsAt < to)
                .OrderBy(x => x.StartsAt)
                .ToList());
        }
    }

    public Task<CalendarEventDto?> GetEventAsync(
        string calendarId,
        string eventId,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_events.SingleOrDefault(x =>
                x.CalendarId == calendarId && x.Id == eventId));
        }
    }

    public async Task<IReadOnlyList<CalendarEventNotificationDto>> GetNotificationsAsync(
        IReadOnlyCollection<string> calendarIds,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var events = await GetEventsAsync(calendarIds, from, to.AddDays(28), cancellationToken);
        return events
            .Select(x => new CalendarEventNotificationDto(
                x,
                x.StartsAt.AddMinutes(-x.NotificationMinutesBefore)))
            .Where(x => x.NotifyAt >= from && x.NotifyAt <= to)
            .ToList();
    }

    public Task<CalendarEventDto> CreateEventAsync(
        CreateCalendarEventRequest request,
        CancellationToken cancellationToken)
    {
        var calendar = _calendars.Single(x => x.Id == request.CalendarId);
        var created = new CalendarEventDto(
            calendar.Id,
            calendar.Name,
            Guid.NewGuid().ToString("N"),
            request.Title,
            request.Description,
            request.Location,
            request.StartsAt,
            request.EndsAt,
            request.IsAllDay,
            calendar.TimeZoneId,
            request.NotificationMinutesBefore,
            null,
            DateTimeOffset.UtcNow);
        lock (_gate)
        {
            _events.Add(created);
        }

        return Task.FromResult(created);
    }

    public Task<CalendarEventDto?> UpdateEventAsync(
        string eventId,
        UpdateCalendarEventRequest request,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var index = _events.FindIndex(x => x.Id == eventId && x.CalendarId == request.CalendarId);
            if (index < 0)
            {
                return Task.FromResult<CalendarEventDto?>(null);
            }

            var existing = _events[index];
            var updated = existing with
            {
                Title = request.Title,
                Description = request.Description,
                Location = request.Location,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                IsAllDay = request.IsAllDay,
                NotificationMinutesBefore = request.NotificationMinutesBefore,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _events[index] = updated;
            return Task.FromResult<CalendarEventDto?>(updated);
        }
    }
}
