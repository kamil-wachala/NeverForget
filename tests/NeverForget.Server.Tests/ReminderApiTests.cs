using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeverForget.Contracts;
using NeverForget.Server.Data;

namespace NeverForget.Server.Tests;

public sealed class ReminderApiTests : IClassFixture<ReminderApiFactory>
{
    private readonly ReminderApiFactory _factory;
    private readonly HttpClient _client;

    public ReminderApiTests(ReminderApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task Reminder_can_be_created_queried_delivered_and_advanced()
    {
        var now = new DateTimeOffset(2026, 9, 8, 12, 0, 30, TimeSpan.Zero);
        _factory.Clock.SetUtcNow(now);
        var createResponse = await _client.PostAsJsonAsync("/api/reminders",
            new CreateReminderRequest("Test", "Remember this", "* * * * *", "UTC"));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ReminderDto>();
        Assert.NotNull(created);
        Assert.Equal(new DateTimeOffset(2026, 9, 8, 12, 1, 0, TimeSpan.Zero), created.NextOccurrence);

        var from = Uri.EscapeDataString(now.ToString("O"));
        var to = Uri.EscapeDataString(now.AddMinutes(3).ToString("O"));
        var range = await _client.GetFromJsonAsync<List<ReminderOccurrenceDto>>(
            $"/api/reminders?from={from}&to={to}");
        Assert.Equal(3, range!.Count(x => x.Reminder.Id == created.Id));

        var due = await _client.GetFromJsonAsync<List<ReminderOccurrenceDto>>("/api/reminders/due");
        Assert.DoesNotContain(due!, x => x.Reminder.Id == created.Id);

        _factory.Clock.SetUtcNow(created.NextOccurrence);
        due = await _client.GetFromJsonAsync<List<ReminderOccurrenceDto>>("/api/reminders/due");
        Assert.Contains(due!, x => x.Reminder.Id == created.Id && x.OccursAt == created.NextOccurrence);

        var acknowledgeResponse = await _client.PostAsync($"/api/reminders/{created.Id}/acknowledge", null);
        Assert.Equal(HttpStatusCode.NoContent, acknowledgeResponse.StatusCode);

        due = await _client.GetFromJsonAsync<List<ReminderOccurrenceDto>>("/api/reminders/due");
        Assert.DoesNotContain(due!, x => x.Reminder.Id == created.Id);

        var advanced = await _client.GetFromJsonAsync<ReminderDto>($"/api/reminders/{created.Id}");
        Assert.Equal(created.NextOccurrence.AddMinutes(1), advanced!.NextOccurrence);
    }

    [Fact]
    public async Task Updating_reminder_recalculates_schedule_and_delete_removes_it()
    {
        var now = new DateTimeOffset(2026, 9, 8, 10, 15, 0, TimeSpan.Zero);
        _factory.Clock.SetUtcNow(now);
        var createResponse = await _client.PostAsJsonAsync("/api/reminders",
            new CreateReminderRequest("Before", "Before edit", "0 * * * *", "UTC"));
        var created = (await createResponse.Content.ReadFromJsonAsync<ReminderDto>())!;

        var updateResponse = await _client.PutAsJsonAsync($"/api/reminders/{created.Id}",
            new UpdateReminderRequest("After", "After edit", "30 9 * * 1-5", "UTC"));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ReminderDto>();
        Assert.Equal("After", updated!.Title);
        Assert.Equal("30 9 * * 1-5", updated.CronExpression);
        Assert.True(updated.NextOccurrence > now);

        var deleteResponse = await _client.DeleteAsync($"/api/reminders/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/reminders/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Invalid_range_returns_bad_request()
    {
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));

        var response = await _client.GetAsync($"/api/reminders?from={from}&to={to}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_reminder_returns_bad_request()
    {
        var response = await _client.PostAsJsonAsync("/api/reminders",
            new CreateReminderRequest(" ", "Valid message", "0 9 * * *", "UTC"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_cron_expression_returns_bad_request()
    {
        var response = await _client.PostAsJsonAsync("/api/reminders",
            new CreateReminderRequest("Invalid cron", "Test", "not a cron expression", "UTC"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public sealed class ReminderApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"neverforget-tests-{Guid.NewGuid()}";
    public ManualTimeProvider Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var databaseDescriptor = services.Single(x =>
                x.ServiceType == typeof(DbContextOptions<NeverForgetDbContext>));
            services.Remove(databaseDescriptor);
            services.AddDbContext<NeverForgetDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            var timeProviderDescriptor = services.Single(x => x.ServiceType == typeof(TimeProvider));
            services.Remove(timeProviderDescriptor);
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}

public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow.ToUniversalTime();
}
