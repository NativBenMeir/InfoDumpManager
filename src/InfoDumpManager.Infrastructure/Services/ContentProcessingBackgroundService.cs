using InfoDumpManager.Application.Agents.Orchestration;
using InfoDumpManager.Application.Infrastructure.JobQueue;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InfoDumpManager.Infrastructure.Services;

/// <summary>
/// Hosted service that drains the processing job queue.
/// </summary>
public sealed class ContentProcessingBackgroundService : BackgroundService
{
    private readonly IJobQueue<ProcessingJob> _jobQueue;
    private readonly IContentProcessingOrchestrator _orchestrator;
    private readonly ILogger<ContentProcessingBackgroundService> _logger;

    public ContentProcessingBackgroundService(
        IJobQueue<ProcessingJob> jobQueue,
        IContentProcessingOrchestrator orchestrator,
        ILogger<ContentProcessingBackgroundService> logger)
    {
        _jobQueue = jobQueue;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Content processing background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var job = await _jobQueue.DequeueAsync(TimeSpan.FromSeconds(5));

            if (job is null)
            {
                continue;
            }

            try
            {
                var result = await _orchestrator.ProcessGEMAsync(
                    job.GEMId,
                    job.TenantId,
                    job.ContentText,
                    job.Options,
                    job.JobId);

                if (result.Status == ProcessingStatus.Completed)
                {
                    await _jobQueue.MarkCompleteAsync(job);
                }
                else
                {
                    var errorMessage = result.Errors.Count > 0
                        ? string.Join("; ", result.Errors)
                        : "Processing failed";

                    await _jobQueue.MarkFailedAsync(job, errorMessage, job.RetryCount);
                }
            }
            catch (Exception ex)
            {
                await _jobQueue.MarkFailedAsync(job, ex.Message, job.RetryCount);
            }
        }

        _logger.LogInformation("Content processing background service stopped.");
    }
}
