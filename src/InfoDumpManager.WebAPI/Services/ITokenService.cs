using System;
using System.Threading.Tasks;
using InfoDumpManager.Infrastructure.Data.Entities;

namespace InfoDumpManager.WebAPI.Services;

public interface ITokenService
{
    Task<(string Token, DateTimeOffset ExpiresAt)> CreateTokenAsync(User user);
}
