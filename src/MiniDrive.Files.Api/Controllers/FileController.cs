using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MiniDrive.Common;
using MiniDrive.Files.DTOs;
using MiniDrive.Files.Services;
using MiniDrive.Clients.Identity;

namespace MiniDrive.Files.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileController : ControllerBase
{
    private readonly IFileService _fileService;
    private readonly IIdentityClient _identityClient;

    public FileController(IFileService fileService, IIdentityClient identityClient)
    {
        _fileService = fileService;
        _identityClient = identityClient;
    }

    /// <summary>
    /// Uploads a file.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(100_000_000)] // 100MB
    public async Task<IActionResult> UploadFile(
        [FromForm] IFormFile file,
        [FromForm] Guid? folderId = null,
        [FromForm] string? description = null,
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided or file is empty." });
        }

        using var stream = file.OpenReadStream();
        var result = await _fileService.UploadFileAsync(
            stream,
            file.FileName,
            file.ContentType,
            userId.Value,
            folderId,
            description,
            GetClientIpAddress(),
            Request.Headers["User-Agent"].ToString());

        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(MapToResponse(result.Value!));
    }

    /// <summary>
    /// Downloads a file by ID.
    /// </summary>
    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadFile(
        Guid id,
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var result = await _fileService.DownloadFileAsync(id, userId.Value);
        if (!result.Succeeded)
        {
            return NotFound(new { error = result.Error });
        }

        var (file, stream) = result.Value!;
        return File(stream, file.ContentType, file.FileName);
    }

    /// <summary>
    /// Gets file metadata by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetFile(
        Guid id,
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var result = await _fileService.GetFileAsync(id, userId.Value);
        if (!result.Succeeded)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(MapToResponse(result.Value!));
    }

    /// <summary>
    /// Lists files for the authenticated user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListFiles(
        [FromQuery] Guid? folderId = null,
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
        var result = await _fileService.ListFilesAsync(userId.Value, folderId, search, pagination);
        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Error });
        }

        var pagedResult = result.Value!;
        var files = pagedResult.Items.Select(MapToResponse).ToList();
        
        return Ok(new
        {
            data = files,
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
    /// Updates file metadata.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateFile(
        Guid id,
        [FromBody] UpdateFileRequest request,
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var result = await _fileService.UpdateFileAsync(
            id,
            userId.Value,
            request.FileName,
            request.Description,
            request.FolderId);

        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(MapToResponse(result.Value!));
    }

    /// <summary>
    /// Deletes a file (soft delete).
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFile(
        Guid id,
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var result = await _fileService.DeleteFileAsync(
            id,
            userId.Value,
            GetClientIpAddress(),
            Request.Headers["User-Agent"].ToString());
        if (!result.Succeeded)
        {
            return NotFound(new { error = result.Error });
        }

        return NoContent();
    }

    /// <summary>
    /// Permanently deletes a file (hard delete).
    /// </summary>
    [HttpDelete("{id}/permanent")]
    public async Task<IActionResult> PermanentlyDeleteFile(
        Guid id,
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var result = await _fileService.PermanentlyDeleteFileAsync(
            id,
            userId.Value,
            GetClientIpAddress(),
            Request.Headers["User-Agent"].ToString());
        if (!result.Succeeded)
        {
            return NotFound(new { error = result.Error });
        }

        return NoContent();
    }

    /// <summary>
    /// Gets total storage used by the authenticated user.
    /// </summary>
    [HttpGet("storage/used")]
    public async Task<IActionResult> GetStorageUsed(
        [FromHeader(Name = "Authorization")] string? authorization = null)
    {
        var userId = await GetUserIdAsync(authorization);
        if (userId == null)
        {
            return Unauthorized(new { error = "Invalid or missing authorization token." });
        }

        var totalBytes = await _fileService.GetTotalStorageUsedAsync(userId.Value);
        return Ok(new { totalBytes, formattedSize = FormatFileSize(totalBytes) });
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

    private static FileResponse MapToResponse(Entities.FileEntry file)
    {
        return new FileResponse
        {
            Id = file.Id,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.SizeBytes,
            Extension = file.Extension,
            OwnerId = file.OwnerId,
            FolderId = file.FolderId,
            Description = file.Description,
            CreatedAtUtc = file.CreatedAtUtc,
            UpdatedAtUtc = file.UpdatedAtUtc,
            FormattedSize = FormatFileSize(file.SizeBytes)
        };
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private string? GetClientIpAddress()
    {
        // Try to get IP from X-Forwarded-For header (for proxies/load balancers)
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ip = forwardedFor.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(ip))
            {
                return ip;
            }
        }

        // Try X-Real-IP header
        var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fall back to connection remote IP
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}

/// <summary>
/// Request DTO for updating a file.
/// </summary>
public class UpdateFileRequest
{
    public string? FileName { get; set; }
    public string? Description { get; set; }
    public Guid? FolderId { get; set; }
}

