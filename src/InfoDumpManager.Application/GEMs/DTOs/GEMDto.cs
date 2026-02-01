using System;

namespace InfoDumpManager.Application.GEMs.DTOs;

public sealed class GEMDto
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public Guid? CategoryId { get; init; }

    public string? CategoryName { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    public string SnapshotHtml { get; init; } = string.Empty;

    public string SnapshotMimeType { get; init; } = string.Empty;

    public DateTimeOffset SnapshotCapturedAt { get; init; }

    public string SourceUrl { get; init; } = string.Empty;

    public string? SourceTitle { get; init; }

    public string SummaryText { get; init; } = string.Empty;

    public string SummaryModel { get; init; } = string.Empty;

    public int SummaryTokenCount { get; init; }
}
