using System;

namespace InfoDumpManager.Domain.Common;

public interface ITenantEntity
{
    Guid TenantId { get; }
}
