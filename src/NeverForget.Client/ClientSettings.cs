using System.IO;
using System.Text.Json;

namespace NeverForget.Client;

public sealed class ClientSettings
{
    public string ServerUrl { get; init; } = "http://localhost:5081/";
    public string ApiKey { get; init; } = string.Empty;
    public int PollingIntervalSeconds { get; init; } = 10;

    public static ClientSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var settings = File.Exists(path)
            ? JsonSerializer.Deserialize<ClientSettings>(File.ReadAllText(path)) ?? new ClientSettings()
            : new ClientSettings();

        return new ClientSettings
        {
            ServerUrl = Environment.GetEnvironmentVariable("NEVERFORGET_API_URL") ?? settings.ServerUrl,
            ApiKey = Environment.GetEnvironmentVariable("NEVERFORGET_API_KEY") ?? settings.ApiKey,
            PollingIntervalSeconds = Math.Clamp(settings.PollingIntervalSeconds, 5, 3600)
        };
    }
}
