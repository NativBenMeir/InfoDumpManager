using System;
using System.Security.Authentication;
using InfoDumpManager.Application.Common.Services;
using Microsoft.AspNetCore.Http;

namespace InfoDumpManager.WebAPI.Services;

public sealed class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId => GetClaimValue("sub");

    public Guid TenantId => GetClaimValue("tenant_id");

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    private Guid GetClaimValue(string claimType)
    {
        var claimValue = _httpContextAccessor.HttpContext?.User?.FindFirst(claimType)?.Value;
        if (Guid.TryParse(claimValue, out var parsed) && parsed != Guid.Empty)
        {
            return parsed;
        }

        throw new AuthenticationException($"Missing or invalid {claimType} claim.");
    }
}
