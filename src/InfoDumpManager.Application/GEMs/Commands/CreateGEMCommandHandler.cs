using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using InfoDumpManager.Application.Agents.Orchestration;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Application.Infrastructure.JobQueue;
using InfoDumpManager.Application.GEMs.DTOs;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
using MediatR;

namespace InfoDumpManager.Application.GEMs.Commands;

public sealed class CreateGEMCommandHandler : IRequestHandler<CreateGEMCommand, CreateGEMCommandResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IMapper _mapper;
    private readonly IDatabasePolicy _databasePolicy;
    private readonly IJobQueue<ProcessingJob> _jobQueue;

    public CreateGEMCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IMapper mapper,
        IDatabasePolicy databasePolicy,
        IJobQueue<ProcessingJob> jobQueue)
    {
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _mapper = mapper;
        _databasePolicy = databasePolicy;
        _jobQueue = jobQueue;
    }

    public async Task<CreateGEMCommandResult> Handle(CreateGEMCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserContext.TenantId;

        var existingGem = await _unitOfWork.GEMs.GetByUrlAsync(tenantId, request.Url, cancellationToken);
        if (existingGem is not null)
        {
            var existingGemDto = _mapper.Map<GEMDto>(existingGem);

            return request.OnDuplicate switch
            {
                CreateGEMOnDuplicateMode.Reject => new CreateGEMCommandResult(
                    CreateGEMOutcome.DuplicateFound,
                    existingGemDto,
                    existingGem.Id,
                    "A GEM with the same URL already exists for the tenant."),
                CreateGEMOnDuplicateMode.UpdateExisting => new CreateGEMCommandResult(
                    CreateGEMOutcome.DuplicateFound,
                    existingGemDto,
                    existingGem.Id,
                    "A GEM with the same URL already exists. Update-existing behavior is not implemented yet."),
                CreateGEMOnDuplicateMode.CreateNewVersion => new CreateGEMCommandResult(
                    CreateGEMOutcome.DuplicateFound,
                    existingGemDto,
                    existingGem.Id,
                    "A GEM with the same URL already exists. Create-new-version behavior is not implemented yet."),
                _ => new CreateGEMCommandResult(
                    CreateGEMOutcome.DuplicateFound,
                    existingGemDto,
                    existingGem.Id,
                    "A GEM with the same URL already exists for the tenant.")
            };
        }

        var source = new GEMSource(request.SourceUrl, request.SourceTitle);
        var snapshot = new GEMSnapshot(
            request.SnapshotHtml,
            request.SnapshotMimeType,
            request.SnapshotCapturedAt,
            request.SnapshotText);
        var summary = ResolveSummary(request);

        var gem = GEM.Create(tenantId, request.Title, request.Url, source, snapshot, summary);

        await _unitOfWork.GEMs.AddAsync(gem, cancellationToken);

        var metadata = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            gem.Id,
            gem.Url,
            gem.Title,
            SourceUrl = source.Url
        }));

        var activityLog = ActivityLog.Create(
            tenantId,
            ActivityEventType.GEMCreated,
            nameof(GEM),
            $"GEM created: {gem.Title}",
            gem.Id,
            _currentUserContext.UserId,
            metadata);

        await _unitOfWork.ActivityLogs.AddAsync(activityLog, cancellationToken);

        await _databasePolicy.ExecuteAsync(() => _unitOfWork.SaveChangesAsync(cancellationToken), cancellationToken);

        var processingContent = string.IsNullOrWhiteSpace(snapshot.TextContent)
            ? snapshot.HtmlContent
            : snapshot.TextContent;

        var job = new ProcessingJob(
            Guid.NewGuid(),
            gem.Id,
            tenantId,
            processingContent,
            new ProcessingOptions(Source: "create-gem"),
            0,
            DateTimeOffset.UtcNow,
            null);

        await _jobQueue.EnqueueAsync(job);

        var createdGem = _mapper.Map<GEMDto>(gem);
        return new CreateGEMCommandResult(CreateGEMOutcome.Created, createdGem, null, null);
    }

    private static GEMSummary ResolveSummary(CreateGEMCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.SummaryText) || string.IsNullOrWhiteSpace(request.SummaryModel))
        {
            return GEMSummary.Empty;
        }

        return GEMSummary.Create(
            request.SummaryText.Trim(),
            request.SummaryModel.Trim(),
            Math.Max(0, request.SummaryTokenCount),
            request.SummaryGeneratedAt ?? DateTimeOffset.UtcNow);
    }
}
