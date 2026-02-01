using System;
using InfoDumpManager.Application.Categories.DTOs;
using MediatR;

namespace InfoDumpManager.Application.Categories.Commands;

public sealed class CreateCategoryCommand : IRequest<CategoryDto>
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }
}
