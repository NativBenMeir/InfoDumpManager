using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Domain.Repositories;
using MediatR;

namespace InfoDumpManager.Application.GEMs.Queries;

public sealed class ListGEMsQueryHandler : IRequestHandler<ListGEMsQuery, ListGEMsResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IMapper _mapper;

    public ListGEMsQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _mapper = mapper;
    }

    public async Task<ListGEMsResult> Handle(ListGEMsQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Max(1, request.PageSize);
        var gems = await _unitOfWork.GEMs.ListByTenantAsync(_currentUserContext.TenantId, cancellationToken);
        var total = gems.Count;

        var items = gems
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(gem => _mapper.Map<InfoDumpManager.Application.GEMs.DTOs.GEMDto>(gem))
            .ToList();

        return new ListGEMsResult(items, pageNumber, pageSize, total);
    }
}
