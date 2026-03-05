using MiniDrive.Common;
using MiniDrive.Folders.Entities;
using MiniDrive.Folders.Repositories;

namespace MiniDrive.Folders.Services;

/// <summary>
/// Service for folder operations (create, read, update, delete).
/// </summary>
public class FolderService : IFolderService
{
    private readonly FolderRepository _folderRepository;

    public FolderService(FolderRepository folderRepository)
    {
        _folderRepository = folderRepository;
    }

    /// <summary>
    /// Creates a new folder.
    /// </summary>
    public async Task<Result<Folder>> CreateFolderAsync(
        string name,
        Guid ownerId,
        Guid? parentFolderId = null,
        string? description = null,
        string? color = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<Folder>.Failure("Folder name cannot be null or empty.");
        }

        // Check if parent folder exists and belongs to the user
        if (parentFolderId.HasValue)
        {
            var parent = await _folderRepository.GetByIdAndOwnerAsync(parentFolderId.Value, ownerId);
            if (parent == null)
            {
                return Result<Folder>.Failure("Parent folder not found or access denied.");
            }
        }

        // Check for duplicate folder name in the same parent
        var existing = await _folderRepository.GetByNameAndParentAsync(name, ownerId, parentFolderId);
        if (existing != null)
        {
            return Result<Folder>.Failure("A folder with this name already exists in the specified location.");
        }

        var folder = new Folder
        {
            Name = name.Trim(),
            OwnerId = ownerId,
            ParentFolderId = parentFolderId,
            Description = description?.Trim(),
            Color = color
        };

        await _folderRepository.CreateAsync(folder);
        return Result<Folder>.Success(folder);
    }

    /// <summary>
    /// Gets a folder by ID.
    /// </summary>
    public async Task<Result<Folder>> GetFolderAsync(Guid folderId, Guid ownerId)
    {
        var folder = await _folderRepository.GetByIdAndOwnerAsync(folderId, ownerId);
        if (folder == null)
        {
            return Result<Folder>.Failure("Folder not found or access denied.");
        }

        return Result<Folder>.Success(folder);
    }

    /// <summary>
    /// Lists folders for a user, optionally filtered by parent.
    /// </summary>
    public async Task<Result<IReadOnlyCollection<Folder>>> ListFoldersAsync(
        Guid ownerId,
        Guid? parentFolderId = null,
        string? searchTerm = null)
    {
        IReadOnlyCollection<Folder> folders;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            folders = await _folderRepository.SearchByOwnerAsync(ownerId, searchTerm, parentFolderId);
        }
        else
        {
            folders = await _folderRepository.GetByOwnerAsync(ownerId, parentFolderId);
        }

        return Result<IReadOnlyCollection<Folder>>.Success(folders);
    }

    /// <summary>
    /// Lists folders for the authenticated user with pagination support, optionally filtered by parent and search term.
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
    public async Task<Result<PagedResult<Folder>>> ListFoldersPagedAsync(
        Guid ownerId,
        Guid? parentFolderId,
        string? searchTerm,
        Pagination pagination)
    {
        PagedResult<Folder> result;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            result = await _folderRepository.SearchByOwnerAsync(ownerId, searchTerm, parentFolderId, pagination);
        }
        else
        {
            result = await _folderRepository.GetByOwnerAsync(ownerId, parentFolderId, pagination);
        }

        return Result<PagedResult<Folder>>.Success(result);
    }

    /// <summary>
    /// Gets the folder hierarchy (breadcrumb path).
    /// </summary>
    public async Task<Result<IReadOnlyCollection<Folder>>> GetFolderPathAsync(Guid folderId, Guid ownerId)
    {
        var path = new List<Folder>();
        var currentId = folderId;

        while (currentId != Guid.Empty)
        {
            var folder = await _folderRepository.GetByIdAndOwnerAsync(currentId, ownerId);
            if (folder == null)
            {
                break;
            }

            path.Insert(0, folder);
            currentId = folder.ParentFolderId ?? Guid.Empty;
        }

        return Result<IReadOnlyCollection<Folder>>.Success(path);
    }

    /// <summary>
    /// Updates folder metadata.
    /// </summary>
    public async Task<Result<Folder>> UpdateFolderAsync(
        Guid folderId,
        Guid ownerId,
        string? name = null,
        string? description = null,
        string? color = null,
        Guid? parentFolderId = null)
    {
        var folder = await _folderRepository.GetByIdAndOwnerAsync(folderId, ownerId);
        if (folder == null)
        {
            return Result<Folder>.Failure("Folder not found or access denied.");
        }

        // Prevent moving folder into itself or its descendants
        if (parentFolderId.HasValue && parentFolderId.Value == folderId)
        {
            return Result<Folder>.Failure("Cannot move folder into itself.");
        }

        if (parentFolderId.HasValue)
        {
            var isDescendant = await IsDescendantAsync(folderId, parentFolderId.Value, ownerId);
            if (isDescendant)
            {
                return Result<Folder>.Failure("Cannot move folder into its own descendant.");
            }

            var parent = await _folderRepository.GetByIdAndOwnerAsync(parentFolderId.Value, ownerId);
            if (parent == null)
            {
                return Result<Folder>.Failure("Parent folder not found or access denied.");
            }
        }

        // Check for duplicate name if name or parent is being changed
        if (!string.IsNullOrWhiteSpace(name) || parentFolderId.HasValue)
        {
            var newName = name ?? folder.Name;
            var newParent = parentFolderId ?? folder.ParentFolderId;
            var existing = await _folderRepository.GetByNameAndParentAsync(newName, ownerId, newParent);
            if (existing != null && existing.Id != folderId)
            {
                return Result<Folder>.Failure("A folder with this name already exists in the specified location.");
            }
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            folder.Name = name.Trim();
        }

        if (description != null)
        {
            folder.Description = description.Trim();
        }

        if (color != null)
        {
            folder.Color = color;
        }

        if (parentFolderId.HasValue)
        {
            folder.ParentFolderId = parentFolderId;
        }

        folder.Touch();

        var updated = await _folderRepository.UpdateAsync(folder);
        if (!updated)
        {
            return Result<Folder>.Failure("Failed to update folder.");
        }

        return Result<Folder>.Success(folder);
    }

    /// <summary>
    /// Deletes a folder (soft delete).
    /// </summary>
    public async Task<Result> DeleteFolderAsync(Guid folderId, Guid ownerId)
    {
        var folder = await _folderRepository.GetByIdAndOwnerAsync(folderId, ownerId);
        if (folder == null)
        {
            return Result.Failure("Folder not found or access denied.");
        }

        // Check if folder has children
        var children = await _folderRepository.GetByOwnerAsync(ownerId, folderId);
        if (children.Any())
        {
            return Result.Failure("Cannot delete folder that contains subfolders. Please delete or move subfolders first.");
        }

        var deleted = await _folderRepository.DeleteAsync(folderId);
        if (!deleted)
        {
            return Result.Failure("Failed to delete folder.");
        }

        return Result.Success();
    }

    /// <summary>
    /// Checks if a folder is a descendant of another folder.
    /// </summary>
    private async Task<bool> IsDescendantAsync(Guid ancestorId, Guid folderId, Guid ownerId)
    {
        var currentId = folderId;
        var depth = 0;
        const int maxDepth = 100; // Prevent infinite loops

        while (currentId != Guid.Empty && depth < maxDepth)
        {
            if (currentId == ancestorId)
            {
                return true;
            }

            var folder = await _folderRepository.GetByIdAndOwnerAsync(currentId, ownerId);
            if (folder == null)
            {
                break;
            }

            currentId = folder.ParentFolderId ?? Guid.Empty;
            depth++;
        }

        return false;
    }
}
