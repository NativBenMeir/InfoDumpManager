using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Application.GEMs.DTOs;
using InfoDumpManager.Domain.Repositories;
using MediatR;

namespace InfoDumpManager.Application.GEMs.Queries;

public sealed class GetGEMByIdQueryHandler : IRequestHandler<GetGEMByIdQuery, GEMDto?>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IMapper _mapper;

    public GetGEMByIdQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _mapper = mapper;
    }

    public async Task<GEMDto?> Handle(GetGEMByIdQuery request, CancellationToken cancellationToken)
    {
        var gem = await _unitOfWork.GEMs.GetByIdAsync(request.GemId, cancellationToken);
        if (gem is null || gem.TenantId != _currentUserContext.TenantId)
        {
            return null;
        }

        return _mapper.Map<GEMDto>(gem);
    }
}
