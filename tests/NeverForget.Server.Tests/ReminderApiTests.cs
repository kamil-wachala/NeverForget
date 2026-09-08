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
    private readonly HttpClient _client;

    public ReminderApiTests(ReminderApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task Reminder_can_be_created_queried_delivered_and_acknowledged()
    {
        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var createResponse = await _client.PostAsJsonAsync("/api/reminders",
            new CreateReminderRequest("Test", "Remember this", scheduledAt));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ReminderDto>();
        Assert.NotNull(created);
        Assert.False(created.IsAcknowledged);

        var from = Uri.EscapeDataString(scheduledAt.AddHours(-1).ToString("O"));
        var to = Uri.EscapeDataString(scheduledAt.AddHours(1).ToString("O"));
        var range = await _client.GetFromJsonAsync<List<ReminderDto>>($"/api/reminders?from={from}&to={to}");
        Assert.Contains(range!, x => x.Id == created.Id);

        var due = await _client.GetFromJsonAsync<List<ReminderDto>>("/api/reminders/due");
        Assert.Contains(due!, x => x.Id == created.Id);

        var acknowledgeResponse = await _client.PostAsync($"/api/reminders/{created.Id}/acknowledge", null);
        Assert.Equal(HttpStatusCode.NoContent, acknowledgeResponse.StatusCode);

        due = await _client.GetFromJsonAsync<List<ReminderDto>>("/api/reminders/due");
        Assert.DoesNotContain(due!, x => x.Id == created.Id);
    }

    [Fact]
    public async Task Updating_reminder_rearms_it_and_delete_removes_it()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/reminders",
            new CreateReminderRequest("Before", "Before edit", DateTimeOffset.UtcNow.AddHours(1)));
        var created = (await createResponse.Content.ReadFromJsonAsync<ReminderDto>())!;

        await _client.PostAsync($"/api/reminders/{created.Id}/acknowledge", null);
        var updateResponse = await _client.PutAsJsonAsync($"/api/reminders/{created.Id}",
            new UpdateReminderRequest("After", "After edit", DateTimeOffset.UtcNow.AddMinutes(30)));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ReminderDto>();
        Assert.Equal("After", updated!.Title);
        Assert.False(updated.IsAcknowledged);

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
            new CreateReminderRequest(" ", "Valid message", DateTimeOffset.UtcNow));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public sealed class ReminderApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"neverforget-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(x => x.ServiceType == typeof(DbContextOptions<NeverForgetDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<NeverForgetDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
