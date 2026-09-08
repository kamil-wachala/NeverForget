using Microsoft.AspNetCore.Mvc;
using NeverForget.Contracts;
using NeverForget.Server.Models;
using NeverForget.Server.Services;

namespace NeverForget.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class RemindersController(IReminderService reminderService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReminderOccurrenceDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReminderOccurrenceDto>>> GetBetween(
        [FromQuery] ReminderRangeRequest range,
        CancellationToken cancellationToken)
    {
        var reminders = await reminderService.GetBetweenAsync(
            range.From!.Value,
            range.To!.Value,
            cancellationToken);
        return Ok(reminders);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ReminderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReminderDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var reminder = await reminderService.GetAsync(id, cancellationToken);
        return reminder is null ? NotFound() : Ok(reminder);
    }

    [HttpGet("due")]
    [ProducesResponseType<IReadOnlyList<ReminderOccurrenceDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReminderOccurrenceDto>>> GetDue(CancellationToken cancellationToken)
    {
        var reminders = await reminderService.GetDueAsync(cancellationToken);
        return Ok(reminders);
    }

    [HttpPost]
    [ProducesResponseType<ReminderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReminderDto>> Create(
        CreateReminderRequest request,
        CancellationToken cancellationToken)
    {
        var reminder = await reminderService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = reminder.Id }, reminder);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ReminderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReminderDto>> Update(
        Guid id,
        UpdateReminderRequest request,
        CancellationToken cancellationToken)
    {
        var reminder = await reminderService.UpdateAsync(id, request, cancellationToken);
        return reminder is null ? NotFound() : Ok(reminder);
    }

    [HttpPost("{id:guid}/acknowledge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken cancellationToken)
    {
        return await reminderService.AcknowledgeAsync(id, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        return await reminderService.DeleteAsync(id, cancellationToken)
            ? NoContent()
            : NotFound();
    }
}
