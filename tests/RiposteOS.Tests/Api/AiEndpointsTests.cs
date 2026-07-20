using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RiposteOS.Api.Ai;
using RiposteOS.Api.Ai.Dtos;
using RiposteOS.Core.Ai;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Api;

public sealed class AiEndpointsTests(RiposteWebApplicationFactory factory) : IClassFixture<RiposteWebApplicationFactory>
{
    [Fact]
    public async Task ProvidersCanBeCreatedListedUpdatedAndDeleted()
    {
        await factory.ResetAsync();
        var client = factory.CreateClient();
        var request = new AiProviderRequest(" Local ", "OpenAiCompatible", "http://localhost:11434/v1", " model ", null, true);

        using var invalid = await client.PostAsJsonAsync("/api/settings/ai/providers", request with { Protocol = "unknown" });
        using var created = await client.PostAsJsonAsync("/api/settings/ai/providers", request);
        var provider = await created.Content.ReadFromJsonAsync<AiProviderResponse>();
        var providers = await client.GetFromJsonAsync<AiProviderResponse[]>("/api/settings/ai/providers");
        using var invalidUpdate = await client.PutAsJsonAsync($"/api/settings/ai/providers/{provider!.Id}", request with { BaseUrl = "relative" });
        using var missingUpdate = await client.PutAsJsonAsync($"/api/settings/ai/providers/{Guid.NewGuid()}", request);
        using var updated = await client.PutAsJsonAsync($"/api/settings/ai/providers/{provider.Id}", request with { Name = "Remote", IsEnabled = false });
        using var deleted = await client.DeleteAsync($"/api/settings/ai/providers/{provider.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Local", provider.Name);
        Assert.Equal("Unknown", provider.HealthStatus);
        Assert.Null(provider.HealthCheckedAt);
        Assert.Single(providers!);
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task TaskAssignmentRequiresAnEnabledProviderAndPreventsItsDeletion()
    {
        await factory.ResetAsync();
        var client = factory.CreateClient();
        var provider = await CreateProviderAsync(client, isEnabled: true);
        var disabled = await CreateProviderAsync(client, isEnabled: false);

        using var invalidTask = await client.PutAsJsonAsync("/api/settings/ai/tasks/unknown", new AiTaskAssignmentRequest(provider.Id));
        using var invalidProvider = await client.PutAsJsonAsync("/api/settings/ai/tasks/DocumentClassification", new AiTaskAssignmentRequest(Guid.Empty));
        using var missingProvider = await client.PutAsJsonAsync("/api/settings/ai/tasks/DocumentClassification", new AiTaskAssignmentRequest(Guid.NewGuid()));
        using var assigned = await client.PutAsJsonAsync("/api/settings/ai/tasks/DocumentClassification", new AiTaskAssignmentRequest(provider.Id));
        var assignment = await client.GetFromJsonAsync<AiTaskAssignmentResponse>("/api/settings/ai/tasks/documentclassification");
        using var disabledAssignment = await client.PutAsJsonAsync("/api/settings/ai/tasks/DocumentClassification", new AiTaskAssignmentRequest(disabled.Id));
        using var deletion = await client.DeleteAsync($"/api/settings/ai/providers/{provider.Id}");
        using var missingTask = await client.GetAsync("/api/settings/ai/tasks/unknown");

        Assert.Equal(HttpStatusCode.BadRequest, invalidTask.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidProvider.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingProvider.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
        Assert.Equal(provider.Id, assignment!.ProviderId);
        Assert.Equal(HttpStatusCode.BadRequest, disabledAssignment.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deletion.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingTask.StatusCode);
    }

    [Fact]
    public async Task ProviderHealthTestReportsMissingAndUnavailableProviders()
    {
        await factory.ResetAsync();
        var client = factory.CreateClient();
        var provider = await CreateProviderAsync(client, isEnabled: true);

        using var missing = await client.PostAsync($"/api/settings/ai/providers/{Guid.NewGuid()}/test", null);
        using var unavailable = await client.PostAsync($"/api/settings/ai/providers/{provider.Id}/test", null);
        var tested = (await client.GetFromJsonAsync<AiProviderResponse[]>("/api/settings/ai/providers"))!.Single();

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, unavailable.StatusCode);
        Assert.Equal("Unavailable", tested.HealthStatus);
        Assert.NotNull(tested.HealthCheckedAt);
    }

    [Fact]
    public async Task ExecutionLogsArePagedAndFilteredWithoutExposingContent()
    {
        await factory.ResetAsync();
        var startedAt = new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
        var completed = new AiExecutionLog(
            AiExecutionOperation.DocumentAnalysis,
            new AiExecutionSubject(AiExecutionSubjectKind.Document, Guid.NewGuid(), "reglement.pdf"),
            Guid.NewGuid(),
            "Docling",
            null,
            null,
            startedAt);
        completed.Complete(startedAt.AddSeconds(2));
        var failed = new AiExecutionLog(
            AiExecutionOperation.DocumentClassification,
            new AiExecutionSubject(AiExecutionSubjectKind.Document, Guid.NewGuid(), "ccap.docx"),
            Guid.NewGuid(),
            "Serveur local",
            "gpt-oss:20b",
            Guid.NewGuid(),
            startedAt.AddMinutes(1));
        failed.Fail("Le classement IA a échoué. Réessayez.", startedAt.AddMinutes(1).AddSeconds(3));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RiposteDbContext>();
            dbContext.AddRange(completed, failed);
            await dbContext.SaveChangesAsync();
            var payload = new AiExecutionPayload(completed.Id, "{\"input\":true}");
            payload.RecordOutput("{\"output\":true}");
            dbContext.Add(payload);
            await dbContext.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var page = await client.GetFromJsonAsync<AiExecutionLogListResponse>(
            "/api/settings/ai/executions?page=1&pageSize=1&filter=status=Completed");
        var detail = await client.GetFromJsonAsync<AiExecutionLogDetailsResponse>(
            $"/api/settings/ai/executions/{completed.Id}");
        using var invalid = await client.GetAsync(
            "/api/settings/ai/executions?page=0");
        using var invalidPageSize = await client.GetAsync(
            "/api/settings/ai/executions?pageSize=101");
        using var invalidFilter = await client.GetAsync(
            $"/api/settings/ai/executions?filter={new string('a', 2_001)}");
        using var invalidOrderBy = await client.GetAsync(
            $"/api/settings/ai/executions?orderBy={new string('a', 201)}");

        var item = Assert.Single(page!.Items);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal("DocumentAnalysis", item.Operation);
        Assert.Equal("Completed", item.Status);
        Assert.Equal("Document", item.SubjectKind);
        Assert.Equal("reglement.pdf", item.SubjectLabel);
        Assert.Null(item.ErrorMessage);
        Assert.Equal("{\"input\":true}", detail!.Input);
        Assert.Equal("{\"output\":true}", detail.Output);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPageSize.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidFilter.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidOrderBy.StatusCode);
    }

    private static async Task<AiProviderResponse> CreateProviderAsync(HttpClient client, bool isEnabled)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/settings/ai/providers",
            new AiProviderRequest(Guid.NewGuid().ToString(), "OpenAiCompatible", "http://localhost:11434/v1", "model", null, isEnabled));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AiProviderResponse>())!;
    }
}
