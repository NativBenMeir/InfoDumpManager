using System.Text.Json;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;

namespace InfoDumpManager.Application.Agents.Orchestration;

/// <summary>
/// Writes ActivityLog entries for processing pipeline steps.
/// </summary>
public sealed class ProcessingActivityLogger : IProcessingActivityLogger
{
    private readonly IUnitOfWork _unitOfWork;

    public ProcessingActivityLogger(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task LogValidationAsync(Guid tenantId, Guid gemId, AgentResult validation, CancellationToken ct = default)
    {
        var metadata = BuildMetadata(new
        {
            gemId,
            status = validation.Data.Payload.TryGetValue("status", out var status) ? status : null,
            response = validation.Data.Payload.TryGetValue("response", out var response) ? response : null,
            confidence = validation.Confidence?.Score
        });

        await _unitOfWork.ActivityLogs.AddAsync(ActivityLog.Create(
            tenantId,
            ActivityEventType.ValidationCompleted,
            nameof(GEM),
            "Validation completed",
            gemId,
            null,
            metadata), ct).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task LogSummarizationAsync(Guid tenantId, Guid gemId, AgentResult summarization, CancellationToken ct = default)
    {
        var metadata = BuildMetadata(new
        {
            gemId,
            model = summarization.Data.Payload.TryGetValue("model", out var model) ? model : null,
            tokenCount = summarization.Data.Payload.TryGetValue("tokenCount", out var tokens) ? tokens : null,
            cacheHit = summarization.Data.Payload.TryGetValue("cacheHit", out var cacheHit) ? cacheHit : null,
            cost = summarization.Metrics.EstimatedCost
        });

        await _unitOfWork.ActivityLogs.AddAsync(ActivityLog.Create(
            tenantId,
            ActivityEventType.SummarizationCompleted,
            nameof(GEM),
            "Summarization completed",
            gemId,
            null,
            metadata), ct).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static JsonDocument BuildMetadata(object payload)
        => JsonDocument.Parse(JsonSerializer.Serialize(payload));
}
