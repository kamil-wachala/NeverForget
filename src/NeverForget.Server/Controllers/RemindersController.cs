using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeverForget.Contracts;
using NeverForget.Server.Data;

namespace NeverForget.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class RemindersController(NeverForgetDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReminderDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReminderDto>>> GetBetween(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (from == default || to == default || from > to)
        {
            ModelState.AddModelError(nameof(from), "Both dates are required and 'from' must not be after 'to'.");
            return ValidationProblem(ModelState);
        }

        var fromUtc = from.UtcDateTime;
        var toUtc = to.UtcDateTime;
        var reminders = await dbContext.Reminders
            .AsNoTracking()
            .Where(x => x.ScheduledAtUtc >= fromUtc && x.ScheduledAtUtc <= toUtc)
            .OrderBy(x => x.ScheduledAtUtc)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);

        return Ok(reminders);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ReminderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReminderDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var reminder = await dbContext.Reminders.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return reminder is null ? NotFound() : Ok(ToDto(reminder));
    }

    [HttpGet("due")]
    [ProducesResponseType<IReadOnlyList<ReminderDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReminderDto>>> GetDue(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var reminders = await dbContext.Reminders
            .AsNoTracking()
            .Where(x => !x.IsAcknowledged && x.ScheduledAtUtc <= nowUtc)
            .OrderBy(x => x.ScheduledAtUtc)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);

        return Ok(reminders);
    }

    [HttpPost]
    [ProducesResponseType<ReminderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReminderDto>> Create(
        CreateReminderRequest request,
        CancellationToken cancellationToken)
    {
        if (!Validate(request.Title, request.Message))
        {
            return ValidationProblem(ModelState);
        }

        var nowUtc = DateTime.UtcNow;
        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            ScheduledAtUtc = request.ScheduledAt.UtcDateTime,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };

        dbContext.Reminders.Add(reminder);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = reminder.Id }, ToDto(reminder));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ReminderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReminderDto>> Update(
        Guid id,
        UpdateReminderRequest request,
        CancellationToken cancellationToken)
    {
        if (!Validate(request.Title, request.Message))
        {
            return ValidationProblem(ModelState);
        }

        var reminder = await dbContext.Reminders.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (reminder is null)
        {
            return NotFound();
        }

        reminder.Title = request.Title.Trim();
        reminder.Message = request.Message.Trim();
        reminder.ScheduledAtUtc = request.ScheduledAt.UtcDateTime;
        reminder.IsAcknowledged = false;
        reminder.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(reminder));
    }

    [HttpPost("{id:guid}/acknowledge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken cancellationToken)
    {
        var reminder = await dbContext.Reminders.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (reminder is null)
        {
            return NotFound();
        }

        reminder.IsAcknowledged = true;
        reminder.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var reminder = await dbContext.Reminders.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (reminder is null)
        {
            return NotFound();
        }

        dbContext.Reminders.Remove(reminder);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private bool Validate(string? title, string? message)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 120)
        {
            ModelState.AddModelError(nameof(title), "Title is required and can have at most 120 characters.");
        }

        if (string.IsNullOrWhiteSpace(message) || message.Trim().Length > 2000)
        {
            ModelState.AddModelError(nameof(message), "Message is required and can have at most 2000 characters.");
        }

        return ModelState.IsValid;
    }

    private static ReminderDto ToDto(Reminder reminder) => new(
        reminder.Id,
        reminder.Title,
        reminder.Message,
        new DateTimeOffset(DateTime.SpecifyKind(reminder.ScheduledAtUtc, DateTimeKind.Utc)),
        reminder.IsAcknowledged,
        new DateTimeOffset(DateTime.SpecifyKind(reminder.CreatedAtUtc, DateTimeKind.Utc)),
        new DateTimeOffset(DateTime.SpecifyKind(reminder.UpdatedAtUtc, DateTimeKind.Utc)));
}
