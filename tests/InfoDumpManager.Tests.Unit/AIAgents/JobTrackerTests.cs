using System;
using System.Threading.Tasks;
using InfoDumpManager.Application.Agents.Orchestration;
using Xunit;

namespace InfoDumpManager.Tests.Unit.AIAgents;

public sealed class JobTrackerTests
{
    [Fact]
    public async Task UpdateStatus_MakesStatusRetrievable()
    {
        var tracker = new InMemoryJobTracker();
        var jobId = Guid.NewGuid();

        tracker.UpdateStatus(jobId, ProcessingStatus.Processing, 50, "Half done");

        var status = await tracker.GetJobStatusAsync(jobId);

        Assert.Equal(ProcessingStatus.Processing, status.Status);
        Assert.Equal(50, status.ProgressPercent);
    }

    [Fact]
    public async Task GetJobStatusAsync_ForUnknownJob_ReturnsPending()
    {
        var tracker = new InMemoryJobTracker();

        var status = await tracker.GetJobStatusAsync(Guid.NewGuid());

        Assert.Equal(ProcessingStatus.Pending, status.Status);
    }
}
