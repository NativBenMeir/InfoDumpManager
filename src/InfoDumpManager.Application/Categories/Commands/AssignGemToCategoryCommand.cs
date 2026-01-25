using System;
using MediatR;

namespace InfoDumpManager.Application.Categories.Commands;

public sealed record AssignGemToCategoryCommand(Guid CategoryId, Guid GemId) : IRequest<bool>;
