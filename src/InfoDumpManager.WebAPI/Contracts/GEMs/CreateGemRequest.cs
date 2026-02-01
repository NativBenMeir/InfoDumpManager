using System;

namespace InfoDumpManager.WebAPI.Contracts.GEMs;

public sealed class CreateGemRequest
{
    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string SourceUrl { get; set; } = string.Empty;

    public string? SourceTitle { get; set; }

    public string SnapshotHtml { get; set; } = string.Empty;

    public string SnapshotMimeType { get; set; } = "text/html";

    public DateTimeOffset? SnapshotCapturedAt { get; set; }

    public string? SummaryText { get; set; }

    public string? SummaryModel { get; set; }

    public int SummaryTokenCount { get; set; }

    public DateTimeOffset? SummaryGeneratedAt { get; set; }
}
