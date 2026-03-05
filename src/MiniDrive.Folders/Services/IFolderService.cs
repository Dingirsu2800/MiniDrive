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
    /// Lists folders for a user with pagination support, optionally filtered by parent and search term.
    /// Returns a <see cref="PagedResult{T}" /> containing the current page of <see cref="Folder" /> items
    /// plus pagination metadata such as total item count and page/page-size information, with stable ordering
    /// so that subsequent pages can be requested reliably.
    /// </summary>
    /// <param name="ownerId">The owner whose folders to list.</param>
    /// <param name="parentFolderId">Optional parent folder to filter by. If <c>null</c>, lists root-level folders.</param>
    /// <param name="searchTerm">Optional search term to filter folders by name or other criteria.</param>
    /// <param name="pagination">Pagination settings (page number and page size) used to select which page to return.</param>
    /// <returns>
    /// A <see cref="Result{T}" /> containing a <see cref="PagedResult{T}" /> of <see cref="Folder" /> items for the requested page,
    /// along with pagination metadata (total number of matching folders, page count, and page information) that callers can
    /// use to retrieve subsequent pages.
    /// </returns>
    Task<Result<PagedResult<Folder>>> ListFoldersPagedAsync(
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
