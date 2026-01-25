using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using InfoDumpManager.Application.GEMs.Commands;
using InfoDumpManager.Application.GEMs.Dtos;
using InfoDumpManager.Application.Services;
using InfoDumpManager.Domain.Common;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
using MediatR;

namespace InfoDumpManager.Application.GEMs.Commands.Handlers;

public sealed class CreateGemCommandHandler : IRequestHandler<CreateGemCommand, GemDto>
{
    private readonly IGEMRepository _gemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IPageSnapshotService _snapshotService;
    private readonly ISnapshotStorageService _storageService;

    public CreateGemCommandHandler(
        IGEMRepository gemRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IPageSnapshotService snapshotService,
        ISnapshotStorageService storageService)
    {
        _gemRepository = gemRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _snapshotService = snapshotService;
        _storageService = storageService;
    }

    public async Task<GemDto> Handle(CreateGemCommand request, CancellationToken cancellationToken)
    {
        var source = GEMSource.Create(request.Url);
        var gem = GEM.Create(source, request.Title);

        var snapshot = await _snapshotService.CaptureAsync(request.Url, cancellationToken);
        gem.AttachSnapshot(GEMSnapshot.Create(snapshot.Content, snapshot.ContentType, snapshot.RetrievedAtUtc));

        await _gemRepository.AddAsync(gem, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var bytes = Encoding.UTF8.GetBytes(snapshot.Content);
        await using var buffer = new MemoryStream(bytes, writable: false);
        await _storageService.StoreSnapshotAsync(
            $"gems/{gem.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}.html",
            buffer,
            snapshot.ContentType,
            cancellationToken);

        return _mapper.Map<GemDto>(gem);
    }
}
