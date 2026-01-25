using System;
using System.Collections.Generic;

namespace InfoDumpManager.Application.Categories.Dtos;

public sealed class CategoryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyList<Guid> GemIds { get; init; } = Array.Empty<Guid>();
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
