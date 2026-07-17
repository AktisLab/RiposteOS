using System.Text.Json;
using RiposteOS.Core.Sourcing;

namespace RiposteOS.Tests.Sourcing;

public sealed class AktislabRelevanceCorpusTests
{
    [Fact]
    public void MatcherKeepsMeasuredPrecisionAndRecallAboveTheAnnotatedBaseline()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Sourcing", "aktislab-relevance-corpus.json");
        var corpus = JsonSerializer.Deserialize<RelevanceCorpus>(
            File.ReadAllText(path),
            JsonSerializerOptions.Web)!;
        var now = new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);
        var settings = new SourcingSettings(new SourcingProfile(
            ["développement logiciel"],
            [],
            corpus.PositiveSignals,
            corpus.NegativeSignals,
            ["FRA"],
            [],
            corpus.CpvWhitelistPrefixes,
            corpus.CpvWatchPrefixes,
            corpus.CpvExcludedPrefixes,
            20,
            15,
            30,
            10,
            25,
            10,
            50,
            7,
            20,
            40,
            SourcingSettings.DefaultSynchronizationCron,
            SourcingSettings.DefaultSynchronizationCron,
            SourcingSettings.DefaultPlaceSynchronizationCron), now);

        var results = corpus.Cases.Select(item => new
        {
            item.SourceId,
            item.ExpectedRelevant,
            Score = SourcingMatcher.Evaluate(
                settings,
                item.Title,
                [],
                item.CpvCodes,
                item.DescriptorLabels,
                null,
                now).Score,
        }).ToArray();
        var truePositives = results.Count(item => item.ExpectedRelevant && item.Score > 0);
        var predictedPositives = results.Count(item => item.Score > 0);
        var actualPositives = results.Count(item => item.ExpectedRelevant);
        var precision = (double)truePositives / predictedPositives;
        var recall = (double)truePositives / actualPositives;
        var errors = string.Join(", ", results
            .Where(item => item.ExpectedRelevant != (item.Score > 0))
            .Select(item => $"{item.SourceId}:{item.Score}"));

        Assert.True(precision >= corpus.MinimumPrecision, $"Precision {precision:P1} is below {corpus.MinimumPrecision:P1}. Errors: {errors}");
        Assert.True(recall >= corpus.MinimumRecall, $"Recall {recall:P1} is below {corpus.MinimumRecall:P1}. Errors: {errors}");
    }

    private sealed record RelevanceCorpus(
        string[] PositiveSignals,
        string[] NegativeSignals,
        string[] CpvWhitelistPrefixes,
        string[] CpvWatchPrefixes,
        string[] CpvExcludedPrefixes,
        double MinimumPrecision,
        double MinimumRecall,
        RelevanceCase[] Cases);

    private sealed record RelevanceCase(
        string SourceId,
        string Title,
        string[] CpvCodes,
        string[] DescriptorLabels,
        bool ExpectedRelevant);
}
