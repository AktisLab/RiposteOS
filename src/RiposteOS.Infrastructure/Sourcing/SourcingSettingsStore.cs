using Microsoft.EntityFrameworkCore;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure.Sourcing;

public sealed class SourcingSettingsStore(
    RiposteDbContext dbContext,
    TimeProvider timeProvider)
{
    public Task<SourcingSettings?> GetAsync(CancellationToken cancellationToken) =>
        dbContext.Set<SourcingSettings>()
            .SingleOrDefaultAsync(item => item.Id == SourcingSettings.DefaultId, cancellationToken);

    public async Task<SourcingSettings> UpdateAsync(
        SourcingProfile profile,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var settings = await GetAsync(cancellationToken);
        if (settings is null)
        {
            settings = new SourcingSettings(profile, now);
            dbContext.Set<SourcingSettings>().Add(settings);
        }
        else
        {
            settings.ChangeProfile(profile, now);
        }

        var opportunities = await dbContext.Set<Opportunity>()
            .ToArrayAsync(cancellationToken);
        foreach (var opportunity in opportunities)
        {
            if (settings.AllowedCountryCodes.Count > 0
                && !opportunity.CountryCodes.Any(country =>
                    settings.AllowedCountryCodes.Contains(
                        country,
                        StringComparer.OrdinalIgnoreCase)))
            {
                dbContext.Remove(opportunity);
                continue;
            }

            var match = SourcingMatcher.Evaluate(
                settings,
                opportunity.Title,
                opportunity.DepartmentCodes,
                opportunity.CpvCodes,
                opportunity.DescriptorLabels,
                opportunity.ResponseDeadline,
                now);
            opportunity.ReassessMatch(match.Score, match.Reasons, now);
        }

        var syncStates = dbContext.Set<SourcingSyncState>();
        syncStates.RemoveRange(syncStates);

        await dbContext.SaveChangesAsync(cancellationToken);

        return settings;
    }
}
