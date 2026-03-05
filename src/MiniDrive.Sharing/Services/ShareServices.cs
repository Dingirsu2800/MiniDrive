using System.Security.Cryptography;
using System.Text;
using MiniDrive.Common;
using MiniDrive.Sharing.DTOs;
using MiniDrive.Sharing.Entities;
using MiniDrive.Sharing.Repositories;

namespace MiniDrive.Sharing.Services;

/// <summary>
/// Service for sharing operations (create, update, delete, manage permissions).
/// </summary>
public class ShareService : IShareService
{
    private readonly ShareRepository _shareRepository;

    public ShareService(ShareRepository shareRepository)
    {
        _shareRepository = shareRepository;
    }

    /// <summary>
    /// Creates a new share.
    /// </summary>
    public async Task<Result<Share>> CreateShareAsync(
        CreateShareRequest request,
        Guid ownerId)
    {
        // Validate inputs
        if (request.ResourceId == Guid.Empty)
            return Result<Share>.Failure("Resource ID cannot be empty.");

        if (string.IsNullOrWhiteSpace(request.ResourceType))
            return Result<Share>.Failure("Resource type is required.");

        var validResourceTypes = new[] { "file", "folder" };
        if (!validResourceTypes.Contains(request.ResourceType.ToLower()))
            return Result<Share>.Failure("Resource type must be 'file' or 'folder'.");

        var validPermissions = new[] { "view", "edit", "admin" };
        if (!validPermissions.Contains(request.Permission?.ToLower() ?? ""))
            return Result<Share>.Failure("Permission must be 'view', 'edit', or 'admin'.");

        if (!request.IsPublicShare && request.SharedWithUserId == null)
            return Result<Share>.Failure("SharedWithUserId is required for non-public shares.");

        // Check if share already exists
        if (!request.IsPublicShare && request.SharedWithUserId.HasValue)
        {
            var existingShare = await _shareRepository.GetShareWithUserAsync(
                request.ResourceId, request.ResourceType, request.SharedWithUserId.Value);
            if (existingShare != null)
                return Result<Share>.Failure("This resource is already shared with this user.");
        }

        var share = new Share
        {
            Id = Guid.NewGuid(),
            ResourceId = request.ResourceId,
            ResourceType = request.ResourceType.ToLower(),
            OwnerId = ownerId,
            SharedWithUserId = request.SharedWithUserId,
            Permission = request.Permission?.ToLower() ?? "view",
            IsPublicShare = request.IsPublicShare,
            IsActive = true,
            ExpiresAtUtc = request.ExpiresAtUtc,
            MaxDownloads = request.MaxDownloads,
            Notes = request.Notes
        };

        // Generate share token for public shares
        if (request.IsPublicShare)
        {
            share.ShareToken = GenerateShareToken();
        }

        // Hash password if provided
        if (!string.IsNullOrEmpty(request.Password))
        {
            share.PasswordHash = HashPassword(request.Password);
        }

        try
        {
            await _shareRepository.CreateAsync(share);
            return Result<Share>.Success(share);
        }
        catch (Exception ex)
        {
            return Result<Share>.Failure($"Failed to create share: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a share by ID (owner verification).
    /// </summary>
    public async Task<Result<Share>> GetShareAsync(Guid shareId, Guid ownerId)
    {
        var share = await _shareRepository.GetByIdAsync(shareId);
        if (share == null)
            return Result<Share>.Failure("Share not found.");

        if (share.OwnerId != ownerId)
            return Result<Share>.Failure("You don't have permission to access this share.");

        return Result<Share>.Success(share);
    }

    /// <summary>
    /// Gets a public share by token without authentication.
    /// </summary>
    public async Task<Result<Share>> GetPublicShareAsync(string token)
    {
        var share = await _shareRepository.GetByShareTokenAsync(token);
        if (share == null)
            return Result<Share>.Failure("Share not found or has expired.");

        // Check expiration
        if (share.ExpiresAtUtc.HasValue && share.ExpiresAtUtc < DateTime.UtcNow)
        {
            share.IsActive = false;
            await _shareRepository.UpdateAsync(share);
            return Result<Share>.Failure("Share has expired.");
        }

        // Check download limit
        if (share.MaxDownloads.HasValue && share.CurrentDownloads >= share.MaxDownloads)
            return Result<Share>.Failure("Download limit reached for this share.");

        return Result<Share>.Success(share);
    }

    /// <summary>
    /// Gets all shares created by a user.
    /// </summary>
    public async Task<Result<IReadOnlyCollection<Share>>> GetUserSharesAsync(Guid ownerId)
    {
        var shares = await _shareRepository.GetByOwnerAsync(ownerId);
        return Result<IReadOnlyCollection<Share>>.Success(shares);
    }

    /// <summary>
    /// Gets all shares with a specific user.
    /// </summary>
    public async Task<Result<IReadOnlyCollection<Share>>> GetSharedWithUserAsync(Guid userId)
    {
        var shares = await _shareRepository.GetBySharedWithUserAsync(userId);
        return Result<IReadOnlyCollection<Share>>.Success(shares);
    }

    /// <summary>
    /// Gets all shares for a specific resource.
    /// </summary>
    public async Task<Result<IReadOnlyCollection<Share>>> GetResourceSharesAsync(
        Guid resourceId,
        string resourceType,
        Guid ownerId)
    {
        // Verify ownership (this would typically involve checking if the user owns the resource)
        // For now, we just get the shares
        var shares = await _shareRepository.GetByResourceAsync(resourceId, resourceType.ToLower());
        
        // Filter to only shares created by this owner
        var userShares = shares.Where(s => s.OwnerId == ownerId).ToList();
        return Result<IReadOnlyCollection<Share>>.Success(userShares);
    }

    /// <summary>
    /// Updates a share.
    /// </summary>
    public async Task<Result<Share>> UpdateShareAsync(
        Guid shareId,
        UpdateShareRequest request,
        Guid ownerId)
    {
        var share = await _shareRepository.GetByIdAsync(shareId);
        if (share == null)
            return Result<Share>.Failure("Share not found.");

        if (share.OwnerId != ownerId)
            return Result<Share>.Failure("You don't have permission to update this share.");

        // Update permission if provided
        if (!string.IsNullOrEmpty(request.Permission))
        {
            var validPermissions = new[] { "view", "edit", "admin" };
            if (!validPermissions.Contains(request.Permission.ToLower()))
                return Result<Share>.Failure("Permission must be 'view', 'edit', or 'admin'.");
            share.Permission = request.Permission.ToLower();
        }

        // Update expiration if provided
        if (request.ExpiresAtUtc != null)
            share.ExpiresAtUtc = request.ExpiresAtUtc;

        // Update active status if provided
        if (request.IsActive.HasValue)
            share.IsActive = request.IsActive.Value;

        // Update password if provided
        if (request.Password != null)
        {
            if (string.IsNullOrEmpty(request.Password))
                share.PasswordHash = null;
            else
                share.PasswordHash = HashPassword(request.Password);
        }

        // Update max downloads if provided
        if (request.MaxDownloads.HasValue)
            share.MaxDownloads = request.MaxDownloads;

        // Update notes if provided
        if (request.Notes != null)
            share.Notes = request.Notes;

        try
        {
            await _shareRepository.UpdateAsync(share);
            return Result<Share>.Success(share);
        }
        catch (Exception ex)
        {
            return Result<Share>.Failure($"Failed to update share: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes a share.
    /// </summary>
    public async Task<Result> DeleteShareAsync(Guid shareId, Guid ownerId)
    {
        var share = await _shareRepository.GetByIdAsync(shareId);
        if (share == null)
            return Result.Failure("Share not found.");

        if (share.OwnerId != ownerId)
            return Result.Failure("You don't have permission to delete this share.");

        try
        {
            await _shareRepository.DeleteAsync(shareId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to delete share: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifies password for a protected public share.
    /// </summary>
    public bool VerifySharePassword(Share share, string password)
    {
        if (string.IsNullOrEmpty(share.PasswordHash))
            return true; // No password protection

        if (string.IsNullOrEmpty(password))
            return false;

        return VerifyPassword(password, share.PasswordHash);
    }

    /// <summary>
    /// Increments download count for a share.
    /// </summary>
    public async Task<Result> IncrementDownloadCountAsync(Guid shareId)
    {
        var share = await _shareRepository.GetByIdAsync(shareId);
        if (share == null)
            return Result.Failure("Share not found.");

        share.CurrentDownloads++;
        try
        {
            await _shareRepository.UpdateAsync(share);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to update download count: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a secure random share token.
    /// </summary>
    private static string GenerateShareToken()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        var token = new StringBuilder();
        for (int i = 0; i < 32; i++)
        {
            token.Append(chars[random.Next(chars.Length)]);
        }
        return token.ToString();
    }

    /// <summary>
    /// Hashes a password using SHA256.
    /// </summary>
    private static string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }

    /// <summary>
    /// Verifies a password against its hash.
    /// </summary>
    private static bool VerifyPassword(string password, string hash)
    {
        var hashOfInput = HashPassword(password);
        return hashOfInput.Equals(hash, StringComparison.OrdinalIgnoreCase);
    }
}

