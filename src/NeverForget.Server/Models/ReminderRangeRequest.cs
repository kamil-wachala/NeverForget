using System.ComponentModel.DataAnnotations;

namespace NeverForget.Server.Models;

public sealed class ReminderRangeRequest : IValidatableObject
{
    [Required]
    public DateTimeOffset? From { get; init; }

    [Required]
    public DateTimeOffset? To { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From.HasValue && To.HasValue && From.Value > To.Value)
        {
            yield return new ValidationResult(
                "'From' must not be after 'To'.",
                [nameof(From), nameof(To)]);
        }
    }
}
