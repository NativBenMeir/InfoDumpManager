using System;
using MediatR;

namespace InfoDumpManager.Application.Categories.Commands;

public sealed record DeleteCategoryCommand(Guid CategoryId) : IRequest<bool>;
