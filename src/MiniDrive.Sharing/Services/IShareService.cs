using MiniDrive.Common;
using MiniDrive.Sharing.DTOs;
using MiniDrive.Sharing.Entities;

namespace MiniDrive.Sharing.Services;

/// <summary>
/// Interface for sharing operations (create, update, delete, manage permissions).
/// </summary>
public interface IShareService
{
    /// <summary>
    /// Creates a new share.
    /// </summary>
    Task<Result<Share>> CreateShareAsync(CreateShareRequest request, Guid ownerId);

    /// <summary>
    /// Gets a share by ID (owner verification).
    /// </summary>
    Task<Result<Share>> GetShareAsync(Guid shareId, Guid ownerId);

    /// <summary>
    /// Gets a public share by token without authentication.
    /// </summary>
    Task<Result<Share>> GetPublicShareAsync(string token);

    /// <summary>
    /// Gets all shares created by a user.
    /// </summary>
    Task<Result<IReadOnlyCollection<Share>>> GetUserSharesAsync(Guid ownerId);

    /// <summary>
    /// Gets all shares with a specific user.
    /// </summary>
    Task<Result<IReadOnlyCollection<Share>>> GetSharedWithUserAsync(Guid userId);

    /// <summary>
    /// Gets all shares for a specific resource.
    /// </summary>
    Task<Result<IReadOnlyCollection<Share>>> GetResourceSharesAsync(
        Guid resourceId,
        string resourceType,
        Guid ownerId);

    /// <summary>
    /// Updates a share.
    /// </summary>
    Task<Result<Share>> UpdateShareAsync(
        Guid shareId,
        UpdateShareRequest request,
        Guid ownerId);

    /// <summary>
    /// Deletes a share.
    /// </summary>
    Task<Result> DeleteShareAsync(Guid shareId, Guid ownerId);

    /// <summary>
    /// Verifies password for a protected public share.
    /// </summary>
    bool VerifySharePassword(Share share, string password);

    /// <summary>
    /// Increments download count for a share.
    /// </summary>
    Task<Result> IncrementDownloadCountAsync(Guid shareId);
}
