using System.Net;
using System.Net.Http.Json;
using Hangfire.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RiposteOS.Api.Sourcing.Dtos;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Infrastructure.Sourcing;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Api;

public sealed class SourcingEndpointsTests(RiposteWebApplicationFactory factory)
    : IClassFixture<RiposteWebApplicationFactory>
{
    [Fact]
    public async Task OpportunitiesAndImportsExposePersistedData()
    {
        await factory.ResetAsync();
        var importedAt = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);
        var opportunity = new Opportunity(
            SourcingSource.Boamp,
            "26-1",
            "Logiciel métier",
            "Acheteur",
            new DateOnly(2026, 7, 15),
            importedAt.AddDays(10),
            ["FRA"],
            ["69"],
            ["72200000"],
            ["186"],
            ["Logiciel"],
            40,
            ["+25 CPV ciblé : 72200000"],
            "https://example.test/26-1",
            "{}",
            importedAt,
            "Description du besoin",
            "open",
            "services",
            250_000m,
            "EUR",
            "24 MONTH",
            "https://example.test/dce");
        var run = new ImportRun(SourcingSource.Boamp, importedAt);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RiposteDbContext>();
            dbContext.Add(opportunity);
            dbContext.Add(run);
            await dbContext.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var opportunities = await client.GetFromJsonAsync<OpportunityListResponse>("/api/opportunities");
        var imports = await client.GetFromJsonAsync<ImportRunListResponse>("/api/sourcing/imports");
        var persistedRun = await client.GetFromJsonAsync<ImportRunResponse>($"/api/sourcing/imports/{run.Id}");
        using var missingRun = await client.GetAsync($"/api/sourcing/imports/{Guid.NewGuid()}");

        var item = Assert.Single(opportunities!.Items);
        Assert.Equal("26-1", item.SourceId);
        Assert.Equal("Description du besoin", item.Description);
        Assert.Equal("open", item.ProcedureType);
        Assert.Equal("services", item.ContractNature);
        Assert.Equal(250_000m, item.EstimatedValue);
        Assert.Equal("EUR", item.Currency);
        Assert.Equal("24 MONTH", item.ExecutionDuration);
        Assert.Equal("https://example.test/dce", item.DocumentUrl);
        Assert.Equal(1, opportunities.TotalCount);
        Assert.Equal(1, opportunities.Page);
        Assert.Equal(20, opportunities.PageSize);
        Assert.Equal(run.Id, Assert.Single(imports!.Items).Id);
        Assert.Equal(1, imports.TotalCount);
        Assert.Equal(run.Id, persistedRun!.Id);
        Assert.Equal(HttpStatusCode.NotFound, missingRun.StatusCode);
    }

    [Fact]
    public async Task ImportHistoryIsPaginatedFromNewestToOldest()
    {
        await factory.ResetAsync();
        var older = new ImportRun(
            SourcingSource.Boamp,
            new DateTimeOffset(2026, 7, 14, 10, 0, 0, TimeSpan.Zero));
        var newer = new ImportRun(
            SourcingSource.Boamp,
            new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RiposteDbContext>();
            dbContext.AddRange(older, newer);
            await dbContext.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var firstPage = await client.GetFromJsonAsync<ImportRunListResponse>(
            "/api/sourcing/imports?page=1&pageSize=1");
        var secondPage = await client.GetFromJsonAsync<ImportRunListResponse>(
            "/api/sourcing/imports?page=2&pageSize=1");

        Assert.Equal(2, firstPage!.TotalCount);
        Assert.Equal(newer.Id, Assert.Single(firstPage.Items).Id);
        Assert.Equal(older.Id, Assert.Single(secondPage!.Items).Id);
    }

    [Fact]
    public async Task ImportIssuesCanBeListedAndRetriedWithoutExposingRawPayload()
    {
        await factory.ResetAsync();
        var now = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
        var run = new ImportRun(SourcingSource.Boamp, now);
        ImportIssue issue;
        ImportIssue invalidIssue;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RiposteDbContext>();
            dbContext.AddRange(
                run,
                new SourcingSettings(TestSourcingProfiles.Create(["logiciel"]), now));
            await dbContext.SaveChangesAsync();
            issue = new ImportIssue(
                run.Id,
                SourcingSource.Boamp,
                "26-retry",
                "mapping_json",
                "{\"idweb\":\"26-retry\",\"objet\":\"Logiciel métier\",\"dateparution\":\"2026-07-16\",\"datelimitereponse\":\"2026-08-16T12:00:00+02:00\",\"secret\":\"never-expose\"}",
                now);
            invalidIssue = new ImportIssue(
                run.Id,
                SourcingSource.Boamp,
                "invalid-retry",
                "mapping_json",
                "{\"idweb\":\"invalid-retry\"}",
                now);
            dbContext.AddRange(issue, invalidIssue);
            await dbContext.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        using var listResponse = await client.GetAsync($"/api/sourcing/imports/{run.Id}/issues");
        var body = await listResponse.Content.ReadAsStringAsync();
        var issues = await listResponse.Content.ReadFromJsonAsync<ImportIssueListResponse>();
        using var retryResponse = await client.PostAsync(
            $"/api/sourcing/import-issues/{issue.Id}/retry",
            null);
        using var invalidPagination = await client.GetAsync(
            $"/api/sourcing/imports/{run.Id}/issues?page=0");
        using var missingRetry = await client.PostAsync(
            $"/api/sourcing/import-issues/{Guid.NewGuid()}/retry",
            null);
        using var unresolvedRetry = await client.PostAsync(
            $"/api/sourcing/import-issues/{invalidIssue.Id}/retry",
            null);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.DoesNotContain("never-expose", body, StringComparison.Ordinal);
        Assert.Equal(2, issues!.TotalCount);
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPagination.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingRetry.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unresolvedRetry.StatusCode);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<RiposteDbContext>();
        Assert.NotNull((await verificationContext.Set<ImportIssue>()
            .SingleAsync(item => item.Id == issue.Id)).ResolvedAt);
        Assert.Equal("26-retry", (await verificationContext.Set<Opportunity>().SingleAsync()).SourceId);
    }

    [Theory]
    [InlineData("/api/sourcing/imports?page=0")]
    [InlineData("/api/sourcing/imports?pageSize=101")]
    public async Task ImportHistoryRejectsInvalidPagination(string uri)
    {
        await factory.ResetAsync();
        var client = factory.CreateClient();

        using var response = await client.GetAsync(uri);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OpportunitiesSupportWhitelistedFilteringOrderingAndPagination()
    {
        await factory.ResetAsync();
        var importedAt = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);
        await SeedOpportunitiesAsync(
            new Opportunity(
                SourcingSource.Boamp,
                "26-2",
                "Portail citoyen",
                "Ville de Lyon",
                new DateOnly(2026, 7, 14),
                importedAt.AddDays(5),
                ["FRA"],
                ["69"],
                ["72400000"],
                ["186"],
                ["Logiciel"],
                20,
                ["+10 CPV surveillé : 72400000"],
                "https://example.test/26-2",
                "{}",
                importedAt),
            new Opportunity(
                SourcingSource.Boamp,
                "26-1",
                "Logiciel métier",
                "Métropole de Lyon",
                new DateOnly(2026, 7, 15),
                importedAt.AddDays(10),
                ["FRA"],
                ["69"],
                ["72200000"],
                ["186"],
                ["Logiciel"],
                50,
                ["+25 CPV ciblé : 72200000"],
                "https://example.test/26-1",
                "{}",
                importedAt));

        var client = factory.CreateClient();
        var firstPage = await client.GetFromJsonAsync<OpportunityListResponse>(
            "/api/opportunities?page=1&pageSize=1&orderBy=title");
        var secondPage = await client.GetFromJsonAsync<OpportunityListResponse>(
            "/api/opportunities?page=2&pageSize=1&orderBy=title");
        var filtered = await client.GetFromJsonAsync<OpportunityListResponse>(
            "/api/opportunities?filter=buyer=*m%C3%A9tropole/i");
        var businessFiltered = await client.GetFromJsonAsync<OpportunityListResponse>(
            "/api/opportunities?departments=69&cpv=722&filter=matchScore%3E=35,status=ToQualify");
        var defaultOrder = await client.GetFromJsonAsync<OpportunityListResponse>(
            "/api/opportunities");

        Assert.Equal(2, firstPage!.TotalCount);
        Assert.Equal("Logiciel métier", Assert.Single(firstPage.Items).Title);
        Assert.Equal("Portail citoyen", Assert.Single(secondPage!.Items).Title);
        Assert.Equal("26-1", Assert.Single(filtered!.Items).SourceId);
        Assert.Equal(1, filtered.TotalCount);
        Assert.Equal("26-1", Assert.Single(businessFiltered!.Items).SourceId);
        Assert.Equal(
            ["26-1", "26-2"],
            defaultOrder!.Items.Select(opportunity => opportunity.SourceId));
    }

    [Fact]
    public async Task OpportunityStatusCanBeDismissedReexaminedAndValidated()
    {
        await factory.ResetAsync();
        var importedAt = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);
        var opportunity = new Opportunity(
            SourcingSource.Boamp,
            "26-status",
            "Logiciel métier",
            "Acheteur",
            new DateOnly(2026, 7, 15),
            importedAt.AddDays(10),
            ["FRA"],
            ["69"],
            ["72200000"],
            ["186"],
            ["Logiciel"],
            50,
            ["+25 CPV ciblé : 72200000"],
            "https://example.test/status",
            "{}",
            importedAt);
        await SeedOpportunitiesAsync(opportunity);
        var client = factory.CreateClient();

        using var retainedResponse = await client.PutAsJsonAsync(
            $"/api/opportunities/{opportunity.Id}/status",
            new OpportunityStatusRequest("Retained"));
        using var dismissedResponse = await client.PutAsJsonAsync(
            $"/api/opportunities/{opportunity.Id}/status",
            new OpportunityStatusRequest("Dismissed"));
        var dismissed = await dismissedResponse.Content.ReadFromJsonAsync<OpportunityListItem>();
        using var toQualifyResponse = await client.PutAsJsonAsync(
            $"/api/opportunities/{opportunity.Id}/status",
            new OpportunityStatusRequest("ToQualify"));
        var toQualify = await toQualifyResponse.Content.ReadFromJsonAsync<OpportunityListItem>();
        using var invalid = await client.PutAsJsonAsync(
            $"/api/opportunities/{opportunity.Id}/status",
            new OpportunityStatusRequest("Unknown"));
        using var missing = await client.PutAsJsonAsync(
            $"/api/opportunities/{Guid.NewGuid()}/status",
            new OpportunityStatusRequest("Dismissed"));

        Assert.Equal(HttpStatusCode.BadRequest, retainedResponse.StatusCode);
        Assert.Equal("Dismissed", dismissed!.Status);
        Assert.Equal("ToQualify", toQualify!.Status);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData("/api/opportunities?page=0")]
    [InlineData("/api/opportunities?pageSize=0")]
    [InlineData("/api/opportunities?pageSize=101")]
    [InlineData("/api/opportunities?filter=rawPayload={}")]
    [InlineData("/api/opportunities?orderBy=rawPayload")]
    public async Task OpportunitiesRejectInvalidOrInternalQueries(string uri)
    {
        await factory.ResetAsync();
        var client = factory.CreateClient();

        using var response = await client.GetAsync(uri);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OpportunitiesRejectOversizedQueries()
    {
        await factory.ResetAsync();
        var client = factory.CreateClient();

        using var oversizedFilter = await client.GetAsync(
            $"/api/opportunities?filter={new string('a', 2_001)}");
        using var oversizedOrdering = await client.GetAsync(
            $"/api/opportunities?orderBy={new string('a', 201)}");

        Assert.Equal(HttpStatusCode.BadRequest, oversizedFilter.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, oversizedOrdering.StatusCode);
    }

    [Fact]
    public async Task ImportRejectsUnknownAndConcurrentSourcesAndQueuesACommand()
    {
        await factory.ResetAsync();
        var client = factory.CreateClient();

        using var unknown = await client.PostAsync("/api/sourcing/unknown/import", null);
        using var profile = await client.PutAsJsonAsync("/api/sourcing/settings", ValidSettings);
        using var accepted = await client.PostAsync("/api/sourcing/boamp/import", null);
        using var acceptedTed = await client.PostAsync("/api/sourcing/ted/import", null);
        using var conflict = await client.PostAsync("/api/sourcing/boamp/import", null);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, acceptedTed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(nameof(SourcingImportJob.ExecuteAsync), factory.Jobs.CreatedJob!.Method.Name);
        var command = Assert.IsType<ImportOpportunities>(factory.Jobs.CreatedJob.Args[0]);
        Assert.Equal(SourcingSource.Ted, command.Source);
    }

    [Fact]
    public async Task ImportIsMarkedAsFailedWhenQueueingFails()
    {
        await factory.ResetAsync();
        factory.Jobs.ThrowOnCreate = true;
        var client = factory.CreateClient();

        using var profile = await client.PutAsJsonAsync("/api/sourcing/settings", ValidSettings);
        using var response = await client.PostAsync("/api/sourcing/boamp/import", null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RiposteDbContext>();
        var run = Assert.Single(dbContext.Set<ImportRun>());
        Assert.Equal(ImportRunStatus.Failed, run.Status);
        Assert.Equal("L'import n'a pas pu être transmis au worker.", run.ErrorMessage);
    }

    [Fact]
    public async Task SettingsCanBeReadAndUpdatedWhenNoImportIsActive()
    {
        await factory.ResetAsync();
        var client = factory.CreateClient();

        var missing = await client.GetFromJsonAsync<SourcingSettingsResponse>("/api/sourcing/settings");
        using var response = await client.PutAsJsonAsync(
            "/api/sourcing/settings",
            ValidSettings with
            {
                Keywords = ["  portail  ", "PORTAIL"],
                ExcludedKeywords = ["porte"],
                PageSize = 25,
                BoampCron = "5 * * * *",
                TedCron = "0 */6 * * *",
                PlaceCron = "30 */6 * * *",
            });
        var updated = await response.Content.ReadFromJsonAsync<SourcingSettingsResponse>();

        Assert.Null(missing);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["portail"], updated!.Keywords);
        Assert.Equal(["porte"], updated.ExcludedKeywords);
        Assert.Equal(["FRA"], updated.AllowedCountryCodes);
        Assert.Equal(25, updated.PageSize);
        Assert.Equal("5 * * * *", updated.BoampCron);
        Assert.Equal("0 */6 * * *", updated.TedCron);
        Assert.Equal("30 */6 * * *", updated.PlaceCron);
        Assert.Collection(
            factory.RecurringJobs.Jobs.OrderBy(job => job.Id),
            job => Assert.Equal(("sourcing-sync-boamp", "5 * * * *", TimeZoneInfo.Utc), job),
            job => Assert.Equal(("sourcing-sync-place", "30 */6 * * *", TimeZoneInfo.Utc), job),
            job => Assert.Equal(("sourcing-sync-ted", "0 */6 * * *", TimeZoneInfo.Utc), job));
    }

    [Fact]
    public async Task SettingsAcceptMissingOptionalLists()
    {
        await factory.ResetAsync();
        var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/api/sourcing/settings",
            ValidSettings with
            {
                ExcludedKeywords = null,
                PositiveSignals = null,
                NegativeSignals = null,
                AllowedCountryCodes = null,
                PreferredDepartmentCodes = null,
                CpvWhitelistPrefixes = null,
                CpvWatchPrefixes = null,
                CpvExcludedPrefixes = null,
                BoampCron = null,
                TedCron = null,
                PlaceCron = null,
            });
        var updated = await response.Content.ReadFromJsonAsync<SourcingSettingsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(updated!.ExcludedKeywords);
        Assert.Empty(updated.PositiveSignals);
        Assert.Equal(["FRA"], updated.AllowedCountryCodes);
        Assert.Empty(updated.PreferredDepartmentCodes);
        Assert.Empty(updated.CpvWhitelistPrefixes);
        Assert.Equal(SourcingSettings.DefaultSynchronizationCron, updated.BoampCron);
        Assert.Equal(SourcingSettings.DefaultSynchronizationCron, updated.TedCron);
        Assert.Equal(SourcingSettings.DefaultPlaceSynchronizationCron, updated.PlaceCron);
    }

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public async Task SettingsValidationRejectsInvalidRequests(SourcingSettingsRequest request)
    {
        await factory.ResetAsync();
        var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync("/api/sourcing/settings", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SettingsCannotChangeDuringAnImport()
    {
        await factory.ResetAsync();
        var client = factory.CreateClient();
        using var profile = await client.PutAsJsonAsync("/api/sourcing/settings", ValidSettings);
        using var import = await client.PostAsync("/api/sourcing/boamp/import", null);

        using var response = await client.PutAsJsonAsync(
            "/api/sourcing/settings",
            ValidSettings with { Keywords = ["logiciel"], PageSize = 10 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ImportRequiresAProfile()
    {
        await factory.ResetAsync();
        var client = factory.CreateClient();

        using var response = await client.PostAsync("/api/sourcing/boamp/import", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Null(factory.Jobs.CreatedJob);
    }

    public static TheoryData<SourcingSettingsRequest> InvalidSettings => new()
    {
        ValidSettings with { Keywords = null },
        ValidSettings with { Keywords = [], PageSize = 101 },
        ValidSettings with { Keywords = Enumerable.Repeat("x", 101).ToArray() },
        ValidSettings with { Keywords = [""] },
        ValidSettings with { Keywords = [new string('a', 101)] },
        ValidSettings with { ExcludedKeywords = Enumerable.Repeat("x", 101).ToArray() },
        ValidSettings with { ExcludedKeywords = [""] },
        ValidSettings with { PreferredDepartmentCodes = ["1234"] },
        ValidSettings with { AllowedCountryCodes = ["FR"] },
        ValidSettings with { AllowedCountryCodes = ["F1A"] },
        ValidSettings with { CpvWhitelistPrefixes = ["abc"] },
        ValidSettings with { PositiveSignalWeight = 101 },
        ValidSettings with { UrgentDeadlineDays = 366 },
        ValidSettings with { BoampCron = "not-a-cron" },
        ValidSettings with { TedCron = "0 0 0 0 0 0" },
        ValidSettings with { PlaceCron = "not-a-cron" },
    };

    private static SourcingSettingsRequest ValidSettings => new(
        ["logiciel"],
        [],
        ["logiciel métier"],
        ["porte automatique"],
        ["FRA"],
        ["69"],
        ["72"],
        ["48000000"],
        [],
        20,
        15,
        30,
        10,
        25,
        10,
        50,
        7,
        20,
        35,
        SourcingSettings.DefaultSynchronizationCron,
        SourcingSettings.DefaultSynchronizationCron,
        SourcingSettings.DefaultPlaceSynchronizationCron);

    private async Task SeedOpportunitiesAsync(params Opportunity[] opportunities)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RiposteDbContext>();
        dbContext.AddRange(opportunities);
        await dbContext.SaveChangesAsync();
    }
}
