using System;

namespace InfoDumpManager.Application.Common.Services;

public interface ICurrentUserContext
{
    Guid UserId { get; }

    Guid TenantId { get; }

    bool IsAuthenticated { get; }
}
