using System;
using MediatR;

namespace InfoDumpManager.Application.GEMs.Commands;

public sealed record UpdateGemCommand(Guid GemId, string? Title) : IRequest<InfoDumpManager.Application.GEMs.Dtos.GemDto?>;
