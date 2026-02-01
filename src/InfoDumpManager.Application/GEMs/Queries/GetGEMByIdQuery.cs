using System;
using InfoDumpManager.Application.GEMs.DTOs;
using MediatR;

namespace InfoDumpManager.Application.GEMs.Queries;

public sealed class GetGEMByIdQuery : IRequest<GEMDto?>
{
    public Guid GemId { get; init; }
}
