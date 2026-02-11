using System.Linq;
using System.Threading.Tasks;
using UserEntity = InfoDumpManager.Infrastructure.Data.Entities.User;
using InfoDumpManager.WebAPI.Contracts.Auth;
using InfoDumpManager.WebAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InfoDumpManager.WebAPI.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<UserEntity> _userManager;
    private readonly SignInManager<UserEntity> _signInManager;
    private readonly ITokenService _tokenService;

    public AuthController(
        UserManager<UserEntity> userManager,
        SignInManager<UserEntity> signInManager,
        ITokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var user = UserEntity.Create(request.TenantId, request.UserName, request.Email, request.DisplayName);
        var creationResult = await _userManager.CreateAsync(user, request.Password);

        if (!creationResult.Succeeded)
        {
            return BadRequest(new
            {
                Title = "Unable to register user",
                Detail = string.Join("; ", creationResult.Errors.Select(e => e.Description)),
                Status = StatusCodes.Status400BadRequest
            });
        }

        var tokenResult = await _tokenService.CreateTokenAsync(user);

        var response = new AuthResponse
        {
            AccessToken = tokenResult.Token,
            ExpiresAt = tokenResult.ExpiresAt
        };

        return Created(string.Empty, response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByNameAsync(request.UserName);
        if (user is null)
        {
            return Unauthorized();
        }

        var attempt = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
        if (!attempt.Succeeded)
        {
            return Unauthorized();
        }

        var tokenResult = await _tokenService.CreateTokenAsync(user);
        var response = new AuthResponse
        {
            AccessToken = tokenResult.Token,
            ExpiresAt = tokenResult.ExpiresAt
        };

        return Ok(response);
    }
}
