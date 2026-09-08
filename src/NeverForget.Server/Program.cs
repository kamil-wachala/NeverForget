using NeverForget.Server.Infrastructure;
using NeverForget.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.Configure<GoogleCalendarOptions>(builder.Configuration.GetSection(GoogleCalendarOptions.SectionName));
builder.Services.AddSingleton<IGoogleCalendarService, GoogleCalendarService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseMiddleware<ApiKeyMiddleware>();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", utcNow = DateTimeOffset.UtcNow }));

app.Run();

public partial class Program;
