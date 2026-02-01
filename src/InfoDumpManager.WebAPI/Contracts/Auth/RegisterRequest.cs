using System;

namespace InfoDumpManager.WebAPI.Contracts.Auth;

public sealed class RegisterRequest
{
    public Guid TenantId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
