using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using InfoDumpManager.Infrastructure.Data.Entities;
using InfoDumpManager.WebAPI.Options;
using Microsoft.IdentityModel.Tokens;

namespace InfoDumpManager.WebAPI.Services;

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;

    public JwtTokenService(JwtSettings jwtSettings)
    {
        _jwtSettings = jwtSettings ?? throw new ArgumentNullException(nameof(jwtSettings));

        if (string.IsNullOrWhiteSpace(_jwtSettings.Secret))
        {
            throw new InvalidOperationException("JWT secret must be configured via JwtSettings:Secret or JWT_SECRET environment variable.");
        }
    }

    public Task<(string Token, DateTimeOffset ExpiresAt)> CreateTokenAsync(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtSettings.ExpiresMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return Task.FromResult((tokenString, expiresAt));
    }
}
