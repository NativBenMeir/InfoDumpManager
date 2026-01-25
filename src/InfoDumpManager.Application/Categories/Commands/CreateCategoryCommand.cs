using MediatR;

namespace InfoDumpManager.Application.Categories.Commands;

public sealed record CreateCategoryCommand(string Name, string? Description) : IRequest<InfoDumpManager.Application.Categories.Dtos.CategoryDto>;
