using MediatR;

namespace InfoDumpManager.Application.GEMs.Commands;

public sealed record CreateGemCommand(string Url, string Title) : IRequest<InfoDumpManager.Application.GEMs.Dtos.GemDto>;
