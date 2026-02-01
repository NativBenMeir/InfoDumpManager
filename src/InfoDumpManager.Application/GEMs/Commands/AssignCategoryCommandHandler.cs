using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using MediatR;

namespace InfoDumpManager.Application.GEMs.Commands;

public sealed class AssignCategoryCommandHandler : IRequestHandler<AssignCategoryCommand, Unit>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IDatabasePolicy _databasePolicy;

    public AssignCategoryCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IDatabasePolicy databasePolicy)
    {
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _databasePolicy = databasePolicy;
    }

    public async Task<Unit> Handle(AssignCategoryCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserContext.TenantId;

        var gem = await _unitOfWork.GEMs.GetByIdAsync(request.GemId, cancellationToken);
        if (gem is null || gem.TenantId != tenantId)
        {
            throw new InvalidOperationException("GEM not found for the current tenant.");
        }

        var category = await _unitOfWork.Categories.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null || category.TenantId != tenantId)
        {
            throw new InvalidOperationException("Category not found for the current tenant.");
        }

        gem.AssignCategory(category);

        var metadata = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            gem.Id,
            CategoryId = category.Id,
            CategoryName = category.Name
        }));

        var activityLog = ActivityLog.Create(
            tenantId,
            ActivityEventType.GEMUpdated,
            nameof(GEM),
            $"GEM updated: category assigned to {category.Name}",
            gem.Id,
            _currentUserContext.UserId,
            metadata);

        await _unitOfWork.ActivityLogs.AddAsync(activityLog, cancellationToken);

        await _databasePolicy.ExecuteAsync(() => _unitOfWork.SaveChangesAsync(cancellationToken), cancellationToken);

        return Unit.Value;
    }
}
