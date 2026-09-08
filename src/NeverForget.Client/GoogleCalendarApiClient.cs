using System.Net.Http;
using System.Net.Http.Json;
using NeverForget.Contracts;

namespace NeverForget.Client;

public sealed class GoogleCalendarApiClient : IDisposable
{
    private readonly HttpClient _httpClient;

    public GoogleCalendarApiClient(string serverUrl, string apiKey)
    {
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var baseAddress))
        {
            throw new ArgumentException("The server address is invalid.", nameof(serverUrl));
        }

        _httpClient = new HttpClient { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(30) };
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }
    }

    public async Task<IReadOnlyList<CalendarDto>> GetCalendarsAsync(
        CancellationToken cancellationToken = default) =>
        await GetJsonAsync<List<CalendarDto>>("api/calendars", cancellationToken) ?? [];

    public async Task<IReadOnlyList<CalendarEventDto>> GetEventsAsync(
        IReadOnlyCollection<string> calendarIds,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var path = BuildRangePath("api/calendar-events", calendarIds, from, to);
        return await GetJsonAsync<List<CalendarEventDto>>(path, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<CalendarEventNotificationDto>> GetNotificationsAsync(
        IReadOnlyCollection<string> calendarIds,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var path = BuildRangePath("api/calendar-events/notifications", calendarIds, from, to);
        return await GetJsonAsync<List<CalendarEventNotificationDto>>(path, cancellationToken) ?? [];
    }

    public async Task<CalendarEventDto> CreateEventAsync(
        CreateCalendarEventRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/calendar-events", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<CalendarEventDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<CalendarEventDto> UpdateEventAsync(
        string eventId,
        UpdateCalendarEventRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            $"api/calendar-events/{Uri.EscapeDataString(eventId)}",
            request,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<CalendarEventDto>(cancellationToken: cancellationToken))!;
    }

    private static string BuildRangePath(
        string path,
        IReadOnlyCollection<string> calendarIds,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        var query = $"from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";
        foreach (var calendarId in calendarIds)
        {
            query += $"&calendarIds={Uri.EscapeDataString(calendarId)}";
        }

        return $"{path}?{query}";
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var details = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"The server returned {(int)response.StatusCode} ({response.ReasonPhrase}). {details}");
    }

    private async Task<T?> GetJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    public void Dispose() => _httpClient.Dispose();
}
