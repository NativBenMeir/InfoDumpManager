using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using InfoDumpManager.Application.GEMs.Dtos;
using InfoDumpManager.Application.GEMs.Queries;
using InfoDumpManager.Domain.Repositories;
using MediatR;

namespace InfoDumpManager.Application.GEMs.Queries.Handlers;

public sealed class GetGemByIdQueryHandler : IRequestHandler<GetGemByIdQuery, GemDto?>
{
    private readonly IGEMRepository _gemRepository;
    private readonly IMapper _mapper;

    public GetGemByIdQueryHandler(IGEMRepository gemRepository, IMapper mapper)
    {
        _gemRepository = gemRepository;
        _mapper = mapper;
    }

    public async Task<GemDto?> Handle(GetGemByIdQuery request, CancellationToken cancellationToken)
    {
        var gem = await _gemRepository.GetByIdAsync(request.GemId, cancellationToken);
        return gem is null ? null : _mapper.Map<GemDto>(gem);
    }
}
