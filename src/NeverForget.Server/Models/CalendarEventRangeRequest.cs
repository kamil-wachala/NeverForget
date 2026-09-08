using System.ComponentModel.DataAnnotations;

namespace NeverForget.Server.Models;

public sealed class CalendarEventRangeRequest : IValidatableObject
{
    [Required]
    public DateTimeOffset? From { get; init; }

    [Required]
    public DateTimeOffset? To { get; init; }

    public string[] CalendarIds { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From is not null && To is not null && From > To)
        {
            yield return new ValidationResult(
                "The start date cannot be later than the end date.",
                [nameof(From), nameof(To)]);
        }
    }
}
