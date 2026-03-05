using Microsoft.AspNetCore.Mvc;
using MiniDrive.Identity.DTOs;
using MiniDrive.Identity.Services;

namespace MiniDrive.Identity.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthServices _authServices;

    public AuthController(IAuthServices authServices)
    {
        _authServices = authServices;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authServices.RegisterAsync(request, GetUserAgent(), GetClientIp());
        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new
        {
            user = new
            {
                result.User!.Id,
                result.User.Email,
                result.User.DisplayName,
                result.User.CreatedAtUtc
            },
            token = new
            {
                accessToken = result.AccessToken,
                expiresAtUtc = result.Session!.ExpiresAtUtc
            }
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authServices.LoginAsync(request, GetUserAgent(), GetClientIp());
        if (!result.Succeeded)
        {
            return Unauthorized(new { error = result.Error });
        }

        return Ok(new
        {
            token = result.AccessToken,
            expiresAtUtc = result.Session!.ExpiresAtUtc,
            user = new
            {
                result.User!.Id,
                result.User.Email,
                result.User.DisplayName,
                result.User.LastLoginAtUtc
            }
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromHeader(Name = "Authorization")] string? authorization)
    {
        var token = ExtractBearerToken(authorization);
        if (token is null)
        {
            return Unauthorized(new { error = "Missing or invalid Authorization header." });
        }

        var removed = await _authServices.LogoutAsync(token);
        if (!removed)
        {
            return NotFound(new { error = "Session not found or already expired." });
        }

        return NoContent();
    }

    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll([FromHeader(Name = "Authorization")] string? authorization)
    {
        var token = ExtractBearerToken(authorization);
        if (token is null)
        {
            return Unauthorized(new { error = "Missing or invalid Authorization header." });
        }

        var user = await _authServices.ValidateSessionAsync(token);
        if (user is null)
        {
            return Unauthorized(new { error = "Session expired or invalid." });
        }

        await _authServices.LogoutAllAsync(token);
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me([FromHeader(Name = "Authorization")] string? authorization)
    {
        var token = ExtractBearerToken(authorization);
        if (token is null)
        {
            return Unauthorized(new { error = "Missing or invalid Authorization header." });
        }

        var user = await _authServices.ValidateSessionAsync(token);
        if (user is null)
        {
            return Unauthorized(new { error = "Session expired or invalid." });
        }

        return Ok(new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            user.LastLoginAtUtc
        });
    }

    private string? ExtractBearerToken(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return null;
        }

        const string prefix = "Bearer ";
        return authorizationHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorizationHeader[prefix.Length..].Trim()
            : authorizationHeader.Trim();
    }

    private string? GetUserAgent() => Request.Headers.UserAgent.ToString();

    private string? GetClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString();
}
