using System;
using MediatR;

namespace InfoDumpManager.Application.Categories.Commands;

public sealed record RemoveGemFromCategoryCommand(Guid CategoryId, Guid GemId) : IRequest<bool>;
