using System;
using MediatR;

namespace InfoDumpManager.Application.GEMs.Commands;

public sealed class AssignCategoryCommand : IRequest<Unit>
{
    public Guid GemId { get; init; }

    public Guid CategoryId { get; init; }
}
