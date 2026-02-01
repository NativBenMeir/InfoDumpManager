using System;
using InfoDumpManager.Application.GEMs.DTOs;
using MediatR;

namespace InfoDumpManager.Application.GEMs.Commands;

public sealed class CreateGEMCommand : IRequest<GEMDto>
{
    public string Title { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string SourceUrl { get; init; } = string.Empty;

    public string? SourceTitle { get; init; }

    public string SnapshotHtml { get; init; } = string.Empty;

    public string SnapshotMimeType { get; init; } = "text/html";

    public DateTimeOffset? SnapshotCapturedAt { get; init; }

    public string? SummaryText { get; init; }

    public string? SummaryModel { get; init; }

    public int SummaryTokenCount { get; init; }

    public DateTimeOffset? SummaryGeneratedAt { get; init; }
}
