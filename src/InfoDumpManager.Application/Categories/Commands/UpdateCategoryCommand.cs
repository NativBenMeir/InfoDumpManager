using System;
using MediatR;

namespace InfoDumpManager.Application.Categories.Commands;

public sealed record UpdateCategoryCommand(Guid CategoryId, string? Name, string? Description) : IRequest<InfoDumpManager.Application.Categories.Dtos.CategoryDto?>;
