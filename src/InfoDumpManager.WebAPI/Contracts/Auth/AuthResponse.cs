using System;

namespace InfoDumpManager.WebAPI.Contracts.Auth;

public sealed class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public string TokenType { get; set; } = "Bearer";
}
