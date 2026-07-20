namespace RiposteOS.Core.Ai;

public sealed class AiTaskAssignment
{
    public AiTaskAssignment(AiTask task, Guid providerId, DateTimeOffset updatedAt)
    {
        Task = Enum.IsDefined(task) ? task : throw new ArgumentOutOfRangeException(nameof(task));
        ProviderId = providerId == Guid.Empty ? throw new ArgumentException("A provider is required.", nameof(providerId)) : providerId;
        UpdatedAt = updatedAt;
    }
    public AiTask Task { get; private set; }
    public Guid ProviderId { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public void Assign(Guid providerId, DateTimeOffset updatedAt) { ProviderId = providerId == Guid.Empty ? throw new ArgumentException("A provider is required.", nameof(providerId)) : providerId; UpdatedAt = updatedAt; }
}
