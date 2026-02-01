using System;
using InfoDumpManager.Application.Common.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InfoDumpManager.Web.Services;

public sealed class WebUserContextOptions
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }
}

public sealed class WebCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly WebUserContextOptions _options;

    public WebCurrentUserContext(IHttpContextAccessor httpContextAccessor, IOptions<WebUserContextOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options?.Value ?? new WebUserContextOptions();
    }

    public Guid UserId => GetClaimOrDefault("sub", _options.UserId);

    public Guid TenantId => GetClaimOrDefault("tenant_id", _options.TenantId);

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    private Guid GetClaimOrDefault(string claimType, Guid fallback)
    {
        var claimValue = _httpContextAccessor.HttpContext?.User?.FindFirst(claimType)?.Value;
        if (Guid.TryParse(claimValue, out var parsed) && parsed != Guid.Empty)
        {
            return parsed;
        }

        if (fallback != Guid.Empty)
        {
            return fallback;
        }

        throw new InvalidOperationException($"Missing or invalid {claimType} claim and no fallback configured.");
    }
}
