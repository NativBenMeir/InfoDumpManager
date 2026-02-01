using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using InfoDumpManager.Application.Categories.DTOs;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using MediatR;

namespace InfoDumpManager.Application.Categories.Commands;

public sealed class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IMapper _mapper;
    private readonly IDatabasePolicy _databasePolicy;

    public CreateCategoryCommandHandler(
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

    public async Task<CategoryDto> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserContext.TenantId;

        if (await _unitOfWork.Categories.ExistsByNameAsync(tenantId, request.Name, cancellationToken))
        {
            throw new InvalidOperationException("A category with the same name already exists for the tenant.");
        }

        var category = Category.Create(tenantId, request.Name, _currentUserContext.UserId, request.Description);

        await _unitOfWork.Categories.AddAsync(category, cancellationToken);

        await _databasePolicy.ExecuteAsync(() => _unitOfWork.SaveChangesAsync(cancellationToken), cancellationToken);

        return _mapper.Map<CategoryDto>(category);
    }
}
