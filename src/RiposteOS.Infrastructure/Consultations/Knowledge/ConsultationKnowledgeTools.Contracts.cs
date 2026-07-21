namespace RiposteOS.Infrastructure.Consultations.Knowledge;

public sealed record ConsultationToolSearchResult(ReferencedPassage[] Passages, bool IsConfigured);
public sealed record ConsultationToolContextResult(string Reference, ReferencedPassage[] Passages);
public sealed record ConsultationToolOutlineResult(Guid DocumentId, ConsultationKnowledgeSection[] Sections);
public sealed record ConsultationToolSectionResult(Guid DocumentId, string SectionTitle, ReferencedPassage[] Passages);
