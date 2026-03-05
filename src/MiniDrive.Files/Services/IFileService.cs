using MiniDrive.Common;
using MiniDrive.Files.Entities;

namespace MiniDrive.Files.Services;

/// <summary>
/// Interface for file operations (upload, download, delete, etc.).
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Uploads a file and creates a file entry.
    /// </summary>
    Task<Result<FileEntry>> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        Guid ownerId,
        Guid? folderId = null,
        string? description = null,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Downloads a file by ID.
    /// </summary>
    Task<Result<(FileEntry File, Stream Content)>> DownloadFileAsync(Guid fileId, Guid ownerId);

    /// <summary>
    /// Gets file metadata by ID.
    /// </summary>
    Task<Result<FileEntry>> GetFileAsync(Guid fileId, Guid ownerId);

    /// <summary>
    /// Lists files for a user, optionally filtered by folder.
    /// </summary>
    Task<Result<IReadOnlyCollection<FileEntry>>> ListFilesAsync(
        Guid ownerId,
        Guid? folderId = null,
        string? searchTerm = null);

    /// <summary>
    /// Lists files for a user with pagination, optionally filtered by folder.
    /// </summary>
    Task<Result<PagedResult<FileEntry>>> ListFilesAsync(
        Guid ownerId,
        Guid? folderId,
        string? searchTerm,
        Pagination pagination);

    /// <summary>
    /// Deletes a file (soft delete).
    /// </summary>
    Task<Result> DeleteFileAsync(
        Guid fileId,
        Guid ownerId,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Permanently deletes a file (hard delete).
    /// </summary>
    Task<Result> PermanentlyDeleteFileAsync(
        Guid fileId,
        Guid ownerId,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Updates file metadata.
    /// </summary>
    Task<Result<FileEntry>> UpdateFileAsync(
        Guid fileId,
        Guid ownerId,
        string? fileName = null,
        string? description = null,
        Guid? folderId = null);

    /// <summary>
    /// Gets total storage used by a user.
    /// </summary>
    Task<long> GetTotalStorageUsedAsync(Guid ownerId);
}
