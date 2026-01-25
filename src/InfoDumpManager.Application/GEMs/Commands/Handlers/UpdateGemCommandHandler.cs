using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using InfoDumpManager.Application.GEMs.Commands;
using InfoDumpManager.Application.GEMs.Dtos;
using InfoDumpManager.Domain.Common;
using InfoDumpManager.Domain.Repositories;
using MediatR;

namespace InfoDumpManager.Application.GEMs.Commands.Handlers;

public sealed class UpdateGemCommandHandler : IRequestHandler<UpdateGemCommand, GemDto?>
{
    private readonly IGEMRepository _gemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public UpdateGemCommandHandler(IGEMRepository gemRepository, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _gemRepository = gemRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<GemDto?> Handle(UpdateGemCommand request, CancellationToken cancellationToken)
    {
        var gem = await _gemRepository.GetByIdAsync(request.GemId, cancellationToken);
        if (gem == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            gem.UpdateTitle(request.Title);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<GemDto>(gem);
    }
}
