using System.Collections.Generic;
using MediatR;
using InfoDumpManager.Application.Categories.Dtos;

namespace InfoDumpManager.Application.Categories.Queries;

public sealed record GetCategoriesQuery() : IRequest<IReadOnlyList<CategoryDto>>;
