using System;
using MediatR;
using InfoDumpManager.Application.Categories.Dtos;

namespace InfoDumpManager.Application.Categories.Queries;

public sealed record GetCategoryByIdQuery(Guid Id) : IRequest<CategoryDto?>;
