using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MiniDrive.Common;
using MiniDrive.Sharing.DTOs;
using MiniDrive.Sharing.Entities;
using MiniDrive.Sharing.Services;

namespace MiniDrive.Sharing.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShareController : ControllerBase
{
    private readonly IShareService _shareService;

    public ShareController(IShareService shareService)
    {
        _shareService = shareService;
    }

    /// <summary>
    /// Creates a new share for a file or folder.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateShare(
        [FromBody] CreateShareRequest request,
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var result = await _shareService.CreateShareAsync(request, userId.Value);
        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Error });
        }

        return CreatedAtAction(nameof(GetShare), new { id = result.Value!.Id }, MapToResponse(result.Value));
    }

    /// <summary>
    /// Gets a share by ID (owner only).
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetShare(
        Guid id,
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var result = await _shareService.GetShareAsync(id, userId.Value);
        if (!result.Succeeded)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(MapToResponse(result.Value!));
    }

    /// <summary>
    /// Gets a public share by token (no authentication required).
    /// </summary>
    [HttpGet("public/{token}")]
    public async Task<IActionResult> GetPublicShare(string token)
    {
        var result = await _shareService.GetPublicShareAsync(token);
        if (!result.Succeeded)
        {
            return NotFound(new { error = result.Error });
        }

        var share = result.Value!;
        return Ok(MapToResponse(share));
    }

    /// <summary>
    /// Gets all shares created by the authenticated user.
    /// </summary>
    [HttpGet("my-shares")]
    public async Task<IActionResult> GetMyShares(
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var result = await _shareService.GetUserSharesAsync(userId.Value);
        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Error });
        }

        var shares = result.Value!.Select(MapToResponse).ToList();
        return Ok(shares);
    }

    /// <summary>
    /// Gets all shares with the authenticated user.
    /// </summary>
    [HttpGet("shared-with-me")]
    public async Task<IActionResult> GetSharedWithMe(
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var result = await _shareService.GetSharedWithUserAsync(userId.Value);
        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Error });
        }

        var shares = result.Value!.Select(MapToResponse).ToList();
        return Ok(shares);
    }

    /// <summary>
    /// Gets all shares for a specific resource.
    /// </summary>
    [HttpGet("resource/{resourceId}")]
    public async Task<IActionResult> GetResourceShares(
        Guid resourceId,
        [FromQuery] string resourceType = "file",
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var result = await _shareService.GetResourceSharesAsync(resourceId, resourceType, userId.Value);
        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Error });
        }

        var shares = result.Value!.Select(MapToResponse).ToList();
        return Ok(shares);
    }

    /// <summary>
    /// Updates a share.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateShare(
        Guid id,
        [FromBody] UpdateShareRequest request,
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var result = await _shareService.UpdateShareAsync(id, request, userId.Value);
        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(MapToResponse(result.Value!));
    }

    /// <summary>
    /// Deletes a share.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteShare(
        Guid id,
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var result = await _shareService.DeleteShareAsync(id, userId.Value);
        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    /// <summary>
    /// Accesses a protected public share with password.
    /// </summary>
    [HttpPost("public/{token}/access")]
    public async Task<IActionResult> AccessProtectedShare(
        string token,
        [FromBody] AccessPublicShareRequest request)
    {
        var result = await _shareService.GetPublicShareAsync(token);
        if (!result.Succeeded)
        {
            return NotFound(new { error = result.Error });
        }

        var share = result.Value!;

        // Verify password if protected
        if (!_shareService.VerifySharePassword(share, request.Password ?? string.Empty))
        {
            return Unauthorized(new { error = "Invalid password." });
        }

        return Ok(MapToResponse(share));
    }

    // Helper methods
    private async Task<Guid?> GetUserIdAsync(string? authorization)
    {
        if (string.IsNullOrEmpty(authorization))
            return null;

        // Extract user ID from token (simplified - in production, validate JWT properly)
        try
        {
            if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authorization.Substring("Bearer ".Length).Trim();
                
                // For now, we assume the token is valid and user is authenticated
                // In a real scenario, validate JWT token here
                // This should be called through IIdentityClient in microservices
                
                // Mock: return a valid GUID for now
                if (!string.IsNullOrEmpty(token))
                {
                    // In production, decode JWT and extract user ID
                    // For testing, we'll assume the token is valid
                    return Guid.TryParse(token.Substring(0, Math.Min(36, token.Length)), out var userId) 
                        ? userId 
                        : Guid.NewGuid(); // Should validate JWT properly
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static ShareResponse MapToResponse(Share share)
    {
        return new ShareResponse
        {
            Id = share.Id,
            ResourceId = share.ResourceId,
            ResourceType = share.ResourceType,
            OwnerId = share.OwnerId,
            SharedWithUserId = share.SharedWithUserId,
            Permission = share.Permission,
            IsPublicShare = share.IsPublicShare,
            ShareToken = share.IsPublicShare ? share.ShareToken : null,
            IsActive = share.IsActive,
            ExpiresAtUtc = share.ExpiresAtUtc,
            HasPassword = !string.IsNullOrEmpty(share.PasswordHash),
            MaxDownloads = share.MaxDownloads,
            CurrentDownloads = share.CurrentDownloads,
            Notes = share.Notes,
            CreatedAtUtc = share.CreatedAtUtc,
            UpdatedAtUtc = share.UpdatedAtUtc
        };
    }
}

