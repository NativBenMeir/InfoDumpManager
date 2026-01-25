using System;
using System.Collections.Generic;

namespace InfoDumpManager.Application.GEMs.Dtos;

public sealed class GemDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string SourceUrl { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public IReadOnlyList<Guid> CategoryIds { get; init; } = Array.Empty<Guid>();
    public string? SnapshotContent { get; init; }
    public string? SnapshotContentType { get; init; }
    public string? SummaryText { get; init; }
}
