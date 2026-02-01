using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Application.GEMs.DTOs;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
using MediatR;

namespace InfoDumpManager.Application.GEMs.Commands;

public sealed class CreateGEMCommandHandler : IRequestHandler<CreateGEMCommand, GEMDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IMapper _mapper;
    private readonly IDatabasePolicy _databasePolicy;

    public CreateGEMCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IMapper mapper,
        IDatabasePolicy databasePolicy)
    {
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _mapper = mapper;
        _databasePolicy = databasePolicy;
    }

    public async Task<GEMDto> Handle(CreateGEMCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserContext.TenantId;

        if (await _unitOfWork.GEMs.ExistsByUrlAsync(tenantId, request.Url, cancellationToken))
        {
            throw new InvalidOperationException("A GEM with the same URL already exists for the tenant.");
        }

        var source = new GEMSource(request.SourceUrl, request.SourceTitle);
        var snapshot = new GEMSnapshot(request.SnapshotHtml, request.SnapshotMimeType, request.SnapshotCapturedAt);
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

        return _mapper.Map<GEMDto>(gem);
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
