using Hangfire;
using Hangfire.Common;
using Hangfire.States;

namespace RiposteOS.Tests.TestSupport;

public sealed class RecordingBackgroundJobClient : IBackgroundJobClient
{
    public Job? CreatedJob { get; private set; }

    public bool ThrowOnCreate { get; set; }

    public string Create(Job job, IState state)
    {
        if (ThrowOnCreate)
        {
            throw new InvalidOperationException("Queue unavailable.");
        }

        CreatedJob = job;
        return Guid.NewGuid().ToString();
    }

    public bool ChangeState(string jobId, IState state, string expectedState) => true;

    public void Reset()
    {
        CreatedJob = null;
        ThrowOnCreate = false;
    }
}
