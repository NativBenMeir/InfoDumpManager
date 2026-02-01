using System.Collections.Generic;
using InfoDumpManager.Application.GEMs.DTOs;
using MediatR;

namespace InfoDumpManager.Application.GEMs.Queries;

public sealed class ListGEMsQuery : IRequest<ListGEMsResult>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record ListGEMsResult(
    IReadOnlyCollection<GEMDto> Items,
    int PageNumber,
    int PageSize,
    int Total);
