using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using InfoDumpManager.Application.Common;
using InfoDumpManager.Application.GEMs.Dtos;
using InfoDumpManager.Application.GEMs.Queries;
using InfoDumpManager.Domain.Repositories;
using MediatR;

namespace InfoDumpManager.Application.GEMs.Queries.Handlers;

public sealed class GetGemsQueryHandler : IRequestHandler<GetGemsQuery, PaginatedResponse<GemDto>>
{
    private readonly IGEMRepository _gemRepository;
    private readonly IMapper _mapper;

    public GetGemsQueryHandler(IGEMRepository gemRepository, IMapper mapper)
    {
        _gemRepository = gemRepository;
        _mapper = mapper;
    }

    public async Task<PaginatedResponse<GemDto>> Handle(GetGemsQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _gemRepository.GetPagedAsync(request.Page, request.PageSize, cancellationToken);
        var dtoItems = _mapper.Map<IReadOnlyList<GemDto>>(items);

        return new PaginatedResponse<GemDto>
        {
            Items = dtoItems.ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
