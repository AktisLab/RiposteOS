using Hangfire;
using Hangfire.Common;

namespace RiposteOS.Tests.TestSupport;

public sealed class RecordingRecurringJobManager : IRecurringJobManager
{
    public List<(string Id, string Cron, TimeZoneInfo TimeZone)> Jobs { get; } = [];

    public void AddOrUpdate(string recurringJobId, Job job, string cronExpression) =>
        AddOrUpdate(recurringJobId, job, cronExpression, new RecurringJobOptions());

    public void AddOrUpdate(
        string recurringJobId,
        Job job,
        string cronExpression,
        RecurringJobOptions options)
    {
        Jobs.RemoveAll(item => item.Id == recurringJobId);
        Jobs.Add((recurringJobId, cronExpression, options.TimeZone));
    }

    public void RemoveIfExists(string recurringJobId)
    {
        Jobs.RemoveAll(item => item.Id == recurringJobId);
    }

    public void Trigger(string recurringJobId)
    {
    }

    public void Reset() => Jobs.Clear();
}
