using MediatR;
using InfoDumpManager.Application.Common;
using InfoDumpManager.Application.GEMs.Dtos;

namespace InfoDumpManager.Application.GEMs.Queries;

public sealed record GetGemsQuery(int Page, int PageSize) : IRequest<PaginatedResponse<GemDto>>;
