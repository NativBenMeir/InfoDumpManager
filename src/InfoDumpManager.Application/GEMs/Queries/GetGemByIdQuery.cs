using System;
using MediatR;
using InfoDumpManager.Application.GEMs.Dtos;

namespace InfoDumpManager.Application.GEMs.Queries;

public sealed record GetGemByIdQuery(Guid GemId) : IRequest<GemDto?>;
