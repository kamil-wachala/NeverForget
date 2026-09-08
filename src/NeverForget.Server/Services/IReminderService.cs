using NeverForget.Contracts;

namespace NeverForget.Server.Services;

public interface IReminderService
{
    Task<IReadOnlyList<ReminderDto>> GetBetweenAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);

    Task<ReminderDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReminderDto>> GetDueAsync(CancellationToken cancellationToken);

    Task<ReminderDto> CreateAsync(
        CreateReminderRequest request,
        CancellationToken cancellationToken);

    Task<ReminderDto?> UpdateAsync(
        Guid id,
        UpdateReminderRequest request,
        CancellationToken cancellationToken);

    Task<bool> AcknowledgeAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
