using System.Text.RegularExpressions;

namespace RiposteOS.Infrastructure.Consultations.Knowledge;

public sealed partial class PassageReferenceSet
{
    private readonly Dictionary<Guid, RegisteredPassage> byPassageId = [];
    private readonly Dictionary<string, RegisteredPassage> byReference = new(StringComparer.Ordinal);

    public int Count => byPassageId.Count;

    public ReferencedPassage[] GetAll() => byReference.Values.Select(ToReference).ToArray();

    public ReferencedPassage Register(ConsultationEvidence evidence)
    {
        if (!byPassageId.TryGetValue(evidence.PassageId, out var registered))
        {
            registered = new RegisteredPassage($"P{byPassageId.Count + 1}", evidence);
            byPassageId.Add(evidence.PassageId, registered);
            byReference.Add(registered.Reference, registered);
        }

        return ToReference(registered);
    }

    public ReferencedPassage[] Register(IEnumerable<ConsultationEvidence> evidence) =>
        evidence.Select(Register).ToArray();

    public bool TryGetPassageId(string reference, out Guid passageId)
    {
        if (byReference.TryGetValue(NormalizeReference(reference), out var registered))
        {
            passageId = registered.Evidence.PassageId;
            return true;
        }

        passageId = Guid.Empty;
        return false;
    }

    public bool TryResolve(IEnumerable<string> references, out ConsultationEvidence[] evidence)
    {
        var normalized = references.Select(NormalizeReference).Distinct(StringComparer.Ordinal).ToArray();
        if (normalized.Any(reference => !byReference.ContainsKey(reference)))
        {
            evidence = [];
            return false;
        }

        evidence = normalized.Select(reference => byReference[reference].Evidence).ToArray();
        return true;
    }

    public bool TryResolveCitations(string answer, out ConsultationEvidence[] evidence) =>
        TryResolve(
            EvidenceCitationRegex().Matches(answer)
                .SelectMany(match => EvidenceReferenceRegex().Matches(match.Groups["references"].Value))
                .Select(match => match.Value),
            out evidence);

    public static string RemoveCitationMarkers(string answer) =>
        EvidenceCitationRegex().Replace(answer, string.Empty).Trim();

    private static string NormalizeReference(string reference) =>
        reference.Trim().TrimStart('[', '(').TrimEnd(']', ')');

    private static ReferencedPassage ToReference(RegisteredPassage registered) => new(
        registered.Reference,
        registered.Evidence.DocumentId,
        registered.Evidence.DocumentName,
        registered.Evidence.PageNumber,
        registered.Evidence.SectionTitle,
        registered.Evidence.Ordinal,
        registered.Evidence.Text);

    [GeneratedRegex(@"P[1-9][0-9]*", RegexOptions.CultureInvariant)]
    private static partial Regex EvidenceReferenceRegex();

    [GeneratedRegex(@" ?[\[(](?<references>P[1-9][0-9]*(?:\s*(?:,|[-–—‑])\s*P[1-9][0-9]*)*)[\])]", RegexOptions.CultureInvariant)]
    private static partial Regex EvidenceCitationRegex();

    private sealed record RegisteredPassage(string Reference, ConsultationEvidence Evidence);
}
