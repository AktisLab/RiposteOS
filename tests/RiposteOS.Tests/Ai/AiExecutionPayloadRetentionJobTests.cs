using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RiposteOS.Core.Ai;
using RiposteOS.Infrastructure.Ai;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Ai;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class AiExecutionPayloadRetentionJobTests(PostgreSqlFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DeletesOnlyExpiredPayloadsAndKeepsExecutionHistory()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await using var dbContext = PostgreSqlFixture.CreateContext(connectionString);
        var expired = CreateExecution(Now.AddDays(-31));
        var retained = CreateExecution(Now.AddDays(-30));
        dbContext.AddRange(expired, retained);
        await dbContext.SaveChangesAsync();
        dbContext.AddRange(
            new AiExecutionPayload(expired.Id, "{\"prompt\":\"expired\"}"),
            new AiExecutionPayload(retained.Id, "{\"prompt\":\"retained\"}"));
        await dbContext.SaveChangesAsync();

        var job = new AiExecutionPayloadRetentionJob(
            dbContext,
            Options.Create(new AiExecutionPayloadRetentionOptions { RetentionDays = 30 }),
            new FixedTimeProvider(Now));

        await job.ExecuteAsync(CancellationToken.None);

        Assert.True(await dbContext.Set<AiExecutionLog>().AnyAsync(log => log.Id == expired.Id));
        Assert.False(await dbContext.Set<AiExecutionPayload>().AnyAsync(payload => payload.ExecutionId == expired.Id));
        Assert.True(await dbContext.Set<AiExecutionPayload>().AnyAsync(payload => payload.ExecutionId == retained.Id));
    }

    [Fact]
    public async Task ExecutionJournalUsesDatabasePagingForPostgreSql()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await using var dbContext = PostgreSqlFixture.CreateContext(connectionString);
        var execution = CreateExecution(Now);
        dbContext.Add(execution);
        await dbContext.SaveChangesAsync();
        var facade = new AiFacade(dbContext, new NoopHealthChecker(), new FixedTimeProvider(Now));

        var result = await facade.ListExecutionLogsAsync(1, 10, null, "startedAt asc", CancellationToken.None);

        Assert.Equal(execution.Id, Assert.Single(result.Items).Id);
        Assert.Empty(result.ValidationErrors);
    }

    private static AiExecutionLog CreateExecution(DateTimeOffset startedAt) => new(
        AiExecutionOperation.DocumentClassification,
        new AiExecutionSubject(AiExecutionSubjectKind.Document, Guid.NewGuid(), "Document de test"),
        null,
        "Provider de test",
        "model-test",
        Guid.NewGuid(),
        startedAt);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class NoopHealthChecker : IAiProviderHealthChecker
    {
        public Task<AiProviderHealthStatus> CheckAsync(AiProvider provider, CancellationToken cancellationToken) =>
            Task.FromResult(AiProviderHealthStatus.Unknown);
    }
}
