using System;

namespace InfoDumpManager.Application.Categories.DTOs;

public sealed class CategoryDto
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public Guid CreatedById { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    public int GemCount { get; init; }
}
