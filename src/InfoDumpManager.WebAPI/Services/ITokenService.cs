using System;
using System.Threading.Tasks;
using InfoDumpManager.Domain.Entities;

namespace InfoDumpManager.WebAPI.Services;

public interface ITokenService
{
    Task<(string Token, DateTimeOffset ExpiresAt)> CreateTokenAsync(User user);
}
