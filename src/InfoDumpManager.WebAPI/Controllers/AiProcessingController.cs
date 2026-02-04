using InfoDumpManager.Application.Agents.Orchestration;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Application.Infrastructure.JobQueue;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.WebAPI.Contracts.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfoDumpManager.WebAPI.Controllers;

[ApiController]
[Authorize(Policy = "MultiTenant")]
[Route("api/ai")]
public sealed class AiProcessingController : ControllerBase
{
    private readonly IJobQueue<ProcessingJob> _jobQueue;
    private readonly IContentProcessingOrchestrator _orchestrator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;

    public AiProcessingController(
        IJobQueue<ProcessingJob> jobQueue,
        IContentProcessingOrchestrator orchestrator,
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext)
    {
        _jobQueue = jobQueue;
        _orchestrator = orchestrator;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessAsync([FromBody] AiProcessRequest request, CancellationToken cancellationToken)
    {
        if (request.GemId == Guid.Empty)
        {
            return BadRequest("GEM id is required.");
        }

        var gem = await _unitOfWork.GEMs.GetByIdAsync(request.GemId, cancellationToken);
        if (gem is null || gem.TenantId != _currentUserContext.TenantId)
        {
            return NotFound();
        }

        var contentText = string.IsNullOrWhiteSpace(request.ContentText)
            ? gem.Snapshot.HtmlContent
            : request.ContentText;

        if (string.IsNullOrWhiteSpace(contentText))
        {
            return BadRequest("Content text is required.");
        }

        var options = new ProcessingOptions(
            request.Source,
            request.AutoApproveThreshold,
            request.RunValidation,
            request.MaxConcurrentJobs,
            request.TimeoutSeconds.HasValue ? TimeSpan.FromSeconds(request.TimeoutSeconds.Value) : null);

        var jobId = Guid.NewGuid();
        var job = new ProcessingJob(
            jobId,
            request.GemId,
            _currentUserContext.TenantId,
            contentText,
            options,
            0,
            DateTimeOffset.UtcNow,
            null);

        await _jobQueue.EnqueueAsync(job);

        var response = new AiProcessResponse
        {
            JobId = jobId,
            Status = ProcessingStatus.Pending
        };

        return AcceptedAtAction("GetJobStatus", new { jobId }, response);
    }

    [HttpGet("jobs/{jobId:guid}")]
    public async Task<IActionResult> GetJobStatusAsync([FromRoute] Guid jobId)
    {
        var status = await _orchestrator.GetJobStatusAsync(jobId);
        return Ok(status);
    }
}
