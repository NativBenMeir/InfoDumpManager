using System;

namespace InfoDumpManager.WebAPI.Contracts.GEMs;

public sealed class AssignCategoryRequest
{
    public Guid CategoryId { get; set; }
}
