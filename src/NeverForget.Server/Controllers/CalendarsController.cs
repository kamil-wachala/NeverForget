using Microsoft.AspNetCore.Mvc;
using NeverForget.Contracts;
using NeverForget.Server.Services;

namespace NeverForget.Server.Controllers;

[ApiController]
[Route("api/calendars")]
public sealed class CalendarsController(IGoogleCalendarService calendarService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CalendarDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CalendarDto>>> Get(CancellationToken cancellationToken) =>
        Ok(await calendarService.GetCalendarsAsync(cancellationToken));
}
