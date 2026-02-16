using System;
using InfoDumpManager.Application.GEMs.DTOs;
using MediatR;

namespace InfoDumpManager.Application.GEMs.Commands;

public enum CreateGEMOnDuplicateMode
{
    Reject = 0,
    UpdateExisting = 1,
    CreateNewVersion = 2
}

public enum CreateGEMOutcome
{
    Created = 0,
    DuplicateFound = 1,
    UpdatedExisting = 2,
    CreatedNewVersion = 3
}

public sealed record CreateGEMCommandResult(
    CreateGEMOutcome Outcome,
    GEMDto? Gem,
    Guid? ExistingGemId,
    string? Message);

public sealed class CreateGEMCommand : IRequest<CreateGEMCommandResult>
{
    public string Title { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string SourceUrl { get; init; } = string.Empty;

    public string? SourceTitle { get; init; }

    public string SnapshotHtml { get; init; } = string.Empty;

    public string? SnapshotText { get; init; }

    public string SnapshotMimeType { get; init; } = "text/html";

    public DateTimeOffset? SnapshotCapturedAt { get; init; }

    public string? SummaryText { get; init; }

    public string? SummaryModel { get; init; }

    public int SummaryTokenCount { get; init; }

    public DateTimeOffset? SummaryGeneratedAt { get; init; }

    public CreateGEMOnDuplicateMode OnDuplicate { get; init; } = CreateGEMOnDuplicateMode.Reject;
}
