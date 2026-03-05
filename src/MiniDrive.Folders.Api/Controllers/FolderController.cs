using Microsoft.AspNetCore.Mvc;
using MiniDrive.Common;
using MiniDrive.Folders.DTOs;
using MiniDrive.Folders.Services;
using MiniDrive.Clients.Identity;

namespace MiniDrive.Folders.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FolderController : ControllerBase
{
    private readonly IFolderService _folderService;
    private readonly IIdentityClient _identityClient;

    public FolderController(IFolderService folderService, IIdentityClient identityClient)
    {
        _folderService = folderService;
        _identityClient = identityClient;
    }

    /// <summary>
    /// Creates a new folder.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateFolder(
        [FromBody] CreateFolderRequest request,
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var result = await _folderService.CreateFolderAsync(
            request.Name,
            userId.Value,
            request.ParentFolderId,
            request.Description,
            request.Color);

        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(MapToResponse(result.Value!));
    }

    /// <summary>
    /// Gets a folder by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetFolder(
        Guid id,
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var result = await _folderService.GetFolderAsync(id, userId.Value);
        if (!result.Succeeded)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(MapToResponse(result.Value!));
    }

    /// <summary>
    /// Lists folders for the authenticated user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListFolders(
        [FromQuery] Guid? parentFolderId = null,
        [FromQuery] string? search = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var pagination = new Pagination(pageNumber, pageSize);
        var result = await _folderService.ListFoldersPagedAsync(userId.Value, parentFolderId, search, pagination);
        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Error });
        }

        var pagedResult = result.Value!;
        var folders = pagedResult.Items.Select(MapToResponse).ToList();
        
        return Ok(new
        {
            data = folders,
            pagination = new
            {
                pageNumber = pagedResult.PageNumber,
                pageSize = pagedResult.PageSize,
                totalCount = pagedResult.TotalCount,
                totalPages = pagedResult.TotalPages,
                hasPreviousPage = pagedResult.HasPreviousPage,
                hasNextPage = pagedResult.HasNextPage
            }
        });
    }

    /// <summary>
    /// Gets the folder path (breadcrumb) for a folder.
    /// </summary>
    [HttpGet("{id}/path")]
    public async Task<IActionResult> GetFolderPath(
        Guid id,
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var result = await _folderService.GetFolderPathAsync(id, userId.Value);
        if (!result.Succeeded)
        {
            return NotFound(new { error = result.Error });
        }

        var folders = result.Value!.Select(MapToResponse).ToList();
        return Ok(folders);
    }

    /// <summary>
    /// Updates folder metadata.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateFolder(
        Guid id,
        [FromBody] UpdateFolderRequest request,
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var result = await _folderService.UpdateFolderAsync(
            id,
            userId.Value,
            request.Name,
            request.Description,
            request.Color,
            request.ParentFolderId);

        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(MapToResponse(result.Value!));
    }

    /// <summary>
    /// Deletes a folder.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFolder(
        Guid id,
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var result = await _folderService.DeleteFolderAsync(id, userId.Value);
        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    private async Task<Guid?> GetUserIdAsync(string? authorization)
    {
        var token = ExtractBearerToken(authorization);
        if (token == null)
        {
            return null;
        }

        var user = await _identityClient.ValidateSessionAsync(token);
        return user?.Id;
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

    private static FolderResponse MapToResponse(Entities.Folder folder)
    {
        return new FolderResponse
        {
            Id = folder.Id,
            Name = folder.Name,
            OwnerId = folder.OwnerId,
            ParentFolderId = folder.ParentFolderId,
            Description = folder.Description,
            Color = folder.Color,
            CreatedAtUtc = folder.CreatedAtUtc,
            UpdatedAtUtc = folder.UpdatedAtUtc
        };
    }
}

