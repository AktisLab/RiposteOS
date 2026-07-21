namespace RiposteOS.Core.Consultations;

public sealed class ConsultationAssistantConversation
{
    public const int MaximumTitleLength = 200;

    public ConsultationAssistantConversation(Guid consultationId, string title, DateTimeOffset createdAt)
    {
        ConsultationId = consultationId == Guid.Empty ? throw new ArgumentException("A consultation is required.", nameof(consultationId)) : consultationId;
        Title = Required(title, MaximumTitleLength, nameof(title));
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid ConsultationId { get; private set; }
    public string Title { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    public void Rename(string title, DateTimeOffset now)
    {
        if (ArchivedAt is not null) throw new InvalidOperationException("An archived conversation cannot be renamed.");
        Title = Required(title, MaximumTitleLength, nameof(title));
        UpdatedAt = now;
    }

    public void Archive(DateTimeOffset now)
    {
        if (ArchivedAt is not null) return;
        ArchivedAt = now;
        UpdatedAt = now;
    }

    private static string Required(string value, int max, string name) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException("A valid value is required.", name) : value.Trim();
}
