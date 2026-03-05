using MiniDrive.Common;
using MiniDrive.Folders.Entities;

namespace MiniDrive.Folders.Services;

/// <summary>
/// Interface for folder operations (create, read, update, delete).
/// </summary>
public interface IFolderService
{
    /// <summary>
    /// Creates a new folder.
    /// </summary>
    Task<Result<Folder>> CreateFolderAsync(
        string name,
        Guid ownerId,
        Guid? parentFolderId = null,
        string? description = null,
        string? color = null);

    /// <summary>
    /// Gets a folder by ID.
    /// </summary>
    Task<Result<Folder>> GetFolderAsync(Guid folderId, Guid ownerId);

    /// <summary>
    /// Lists folders for a user, optionally filtered by parent.
    /// </summary>
    Task<Result<IReadOnlyCollection<Folder>>> ListFoldersAsync(
        Guid ownerId,
        Guid? parentFolderId = null,
        string? searchTerm = null);

    /// <summary>
    /// Lists folders for a user with pagination support, optionally filtered by parent.
    /// </summary>
    Task<Result<IReadOnlyCollection<Folder>>> ListFoldersAsync(
        Guid ownerId,
        Guid? parentFolderId,
        string? searchTerm,
        Pagination pagination);

    /// <summary>
    /// Gets the folder hierarchy (breadcrumb path).
    /// </summary>
    Task<Result<IReadOnlyCollection<Folder>>> GetFolderPathAsync(Guid folderId, Guid ownerId);

    /// <summary>
    /// Updates folder metadata.
    /// </summary>
    Task<Result<Folder>> UpdateFolderAsync(
        Guid folderId,
        Guid ownerId,
        string? name = null,
        string? description = null,
        string? color = null,
        Guid? parentFolderId = null);

    /// <summary>
    /// Deletes a folder (soft delete).
    /// </summary>
    Task<Result> DeleteFolderAsync(Guid folderId, Guid ownerId);
}
