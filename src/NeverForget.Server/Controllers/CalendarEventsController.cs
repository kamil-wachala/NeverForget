using Microsoft.AspNetCore.Mvc;
using NeverForget.Contracts;
using NeverForget.Server.Models;
using NeverForget.Server.Services;

namespace NeverForget.Server.Controllers;

[ApiController]
[Route("api/calendar-events")]
public sealed class CalendarEventsController(IGoogleCalendarService calendarService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CalendarEventDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CalendarEventDto>>> GetBetween(
        [FromQuery] CalendarEventRangeRequest range,
        CancellationToken cancellationToken) =>
        Ok(await calendarService.GetEventsAsync(
            range.CalendarIds,
            range.From!.Value,
            range.To!.Value,
            cancellationToken));

    [HttpGet("{eventId}")]
    [ProducesResponseType<CalendarEventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CalendarEventDto>> Get(
        string eventId,
        [FromQuery] string calendarId,
        CancellationToken cancellationToken)
    {
        var calendarEvent = await calendarService.GetEventAsync(calendarId, eventId, cancellationToken);
        return calendarEvent is null ? NotFound() : Ok(calendarEvent);
    }

    [HttpGet("notifications")]
    [ProducesResponseType<IReadOnlyList<CalendarEventNotificationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CalendarEventNotificationDto>>> GetNotifications(
        [FromQuery] CalendarEventRangeRequest range,
        CancellationToken cancellationToken) =>
        Ok(await calendarService.GetNotificationsAsync(
            range.CalendarIds,
            range.From!.Value,
            range.To!.Value,
            cancellationToken));

    [HttpPost]
    [ProducesResponseType<CalendarEventDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CalendarEventDto>> Create(
        CreateCalendarEventRequest request,
        CancellationToken cancellationToken)
    {
        var calendarEvent = await calendarService.CreateEventAsync(request, cancellationToken);
        return CreatedAtAction(
            nameof(Get),
            new { eventId = calendarEvent.Id, calendarId = calendarEvent.CalendarId },
            calendarEvent);
    }

    [HttpPut("{eventId}")]
    [ProducesResponseType<CalendarEventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CalendarEventDto>> Update(
        string eventId,
        UpdateCalendarEventRequest request,
        CancellationToken cancellationToken)
    {
        var calendarEvent = await calendarService.UpdateEventAsync(eventId, request, cancellationToken);
        return calendarEvent is null ? NotFound() : Ok(calendarEvent);
    }
}
