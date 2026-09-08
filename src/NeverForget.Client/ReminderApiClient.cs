using System.Net.Http;
using System.Net.Http.Json;
using NeverForget.Contracts;

namespace NeverForget.Client;

public sealed class ReminderApiClient : IDisposable
{
    private readonly HttpClient _httpClient;

    public ReminderApiClient(string serverUrl, string apiKey)
    {
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var baseAddress))
        {
            throw new ArgumentException("The server address is invalid.", nameof(serverUrl));
        }

        _httpClient = new HttpClient { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(90) };
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }
    }

    public async Task<IReadOnlyList<ReminderDto>> GetBetweenAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var path = $"api/reminders?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";
        return await _httpClient.GetFromJsonAsync<List<ReminderDto>>(path, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<ReminderDto>> GetDueAsync(CancellationToken cancellationToken = default) =>
        await _httpClient.GetFromJsonAsync<List<ReminderDto>>("api/reminders/due", cancellationToken) ?? [];

    public async Task<ReminderDto> CreateAsync(
        CreateReminderRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/reminders", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ReminderDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<ReminderDto> UpdateAsync(
        Guid id,
        UpdateReminderRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync($"api/reminders/{id}", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ReminderDto>(cancellationToken: cancellationToken))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync($"api/reminders/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task AcknowledgeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"api/reminders/{id}/acknowledge", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var details = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"The server returned {(int)response.StatusCode} ({response.ReasonPhrase}). {details}");
    }

    public void Dispose() => _httpClient.Dispose();
}
