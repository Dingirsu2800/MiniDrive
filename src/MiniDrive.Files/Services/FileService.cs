using MiniDrive.Audit.Services;
using MiniDrive.Common;
using MiniDrive.Files.DTOs;
using MiniDrive.Files.Entities;
using MiniDrive.Files.Repositories;
using MiniDrive.Files.Validators;
using MiniDrive.Quota.Services;
using MiniDrive.Storage;

namespace MiniDrive.Files.Services;

/// <summary>
/// Service for file operations (upload, download, delete, etc.).
/// </summary>
public class FileService : IFileService
{
    private readonly FileRepository _fileRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IQuotaService _quotaService;
    private readonly IAuditService _auditService;

    public FileService(
        FileRepository fileRepository,
        IFileStorage fileStorage,
        IQuotaService quotaService,
        IAuditService auditService)
    {
        _fileRepository = fileRepository;
        _fileStorage = fileStorage;
        _quotaService = quotaService;
        _auditService = auditService;
    }

    /// <summary>
    /// Uploads a file and creates a file entry.
    /// </summary>
    public async Task<Result<FileEntry>> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        Guid ownerId,
        Guid? folderId = null,
        string? description = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        if (fileStream == null || fileStream.Length == 0)
        {
            await _auditService.LogActionAsync(
                ownerId,
                "FileUpload",
                "File",
                Guid.Empty.ToString(),
                false,
                $"File: {fileName}",
                "File stream cannot be null or empty.",
                ipAddress,
                userAgent);
            return Result<FileEntry>.Failure("File stream cannot be null or empty.");
        }

        // Validate file name for security issues
        var fileNameValidation = FileNameValidator.ValidateFileName(fileName);
        if (!fileNameValidation.Succeeded)
        {
            await _auditService.LogActionAsync(
                ownerId,
                "FileUpload",
                "File",
                Guid.Empty.ToString(),
                false,
                $"File: {fileName}",
                fileNameValidation.Error,
                ipAddress,
                userAgent);
            return Result<FileEntry>.Failure(fileNameValidation.Error);
        }

        // Validate description if provided
        if (!string.IsNullOrWhiteSpace(description))
        {
            var descriptionValidation = FileNameValidator.ValidateDescription(description);
            if (!descriptionValidation.Succeeded)
            {
                await _auditService.LogActionAsync(
                    ownerId,
                    "FileUpload",
                    "File",
                    Guid.Empty.ToString(),
                    false,
                    $"File: {fileName}",
                    descriptionValidation.Error,
                    ipAddress,
                    userAgent);
                return Result<FileEntry>.Failure(descriptionValidation.Error);
            }
        }

        // Check quota before upload
        var canUpload = await _quotaService.CanUploadAsync(ownerId, fileStream.Length);
        if (!canUpload)
        {
            var quota = await _quotaService.GetQuotaAsync(ownerId);
            var errorMessage = quota != null
                ? $"Storage quota exceeded. Used: {quota.UsedBytes} bytes, Limit: {quota.LimitBytes} bytes, Available: {quota.AvailableBytes} bytes"
                : "Storage quota exceeded.";

            await _auditService.LogActionAsync(
                ownerId,
                "FileUpload",
                "File",
                Guid.Empty.ToString(),
                false,
                $"File: {fileName}, Size: {fileStream.Length} bytes",
                errorMessage,
                ipAddress,
                userAgent);
            return Result<FileEntry>.Failure(errorMessage);
        }

        try
        {
            // Save file to storage
            var storagePath = await _fileStorage.SaveAsync(fileStream, fileName);

            // Create file entry
            var fileEntry = new FileEntry
            {
                FileName = fileName,
                ContentType = contentType,
                SizeBytes = fileStream.Length,
                StoragePath = storagePath,
                OwnerId = ownerId,
                FolderId = folderId,
                Extension = Path.GetExtension(fileName),
                Description = description
            };

            await _fileRepository.CreateAsync(fileEntry);

            // Update quota
            await _quotaService.IncreaseAsync(ownerId, fileStream.Length);

            // Log successful upload
            await _auditService.LogActionAsync(
                ownerId,
                "FileUpload",
                "File",
                fileEntry.Id.ToString(),
                true,
                $"File: {fileName}, Size: {fileStream.Length} bytes, ContentType: {contentType}",
                null,
                ipAddress,
                userAgent);

            return Result<FileEntry>.Success(fileEntry);
        }
        catch (Exception ex)
        {
            await _auditService.LogActionAsync(
                ownerId,
                "FileUpload",
                "File",
                Guid.Empty.ToString(),
                false,
                $"File: {fileName}, Size: {fileStream.Length} bytes",
                ex.Message,
                ipAddress,
                userAgent);
            return Result<FileEntry>.Failure($"Failed to upload file: {ex.Message}");
        }
    }

    /// <summary>
    /// Downloads a file by ID.
    /// </summary>
    public async Task<Result<(FileEntry File, Stream Content)>> DownloadFileAsync(Guid fileId, Guid ownerId)
    {
        var file = await _fileRepository.GetByIdAndOwnerAsync(fileId, ownerId);
        if (file == null)
        {
            return Result<(FileEntry, Stream)>.Failure("File not found or access denied.");
        }

        try
        {
            var stream = await _fileStorage.GetAsync(file.StoragePath);
            return Result<(FileEntry, Stream)>.Success((file, stream));
        }
        catch (Exception ex)
        {
            return Result<(FileEntry, Stream)>.Failure($"Failed to retrieve file: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets file metadata by ID.
    /// </summary>
    public async Task<Result<FileEntry>> GetFileAsync(Guid fileId, Guid ownerId)
    {
        var file = await _fileRepository.GetByIdAndOwnerAsync(fileId, ownerId);
        if (file == null)
        {
            return Result<FileEntry>.Failure("File not found or access denied.");
        }

        return Result<FileEntry>.Success(file);
    }

    /// <summary>
    /// Lists files for a user, optionally filtered by folder.
    /// </summary>
    public async Task<Result<IReadOnlyCollection<FileEntry>>> ListFilesAsync(
        Guid ownerId,
        Guid? folderId = null,
        string? searchTerm = null)
    {
        // Validate search term for security issues
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchValidation = FileNameValidator.ValidateSearchTerm(searchTerm);
            if (!searchValidation.Succeeded)
            {
                return Result<IReadOnlyCollection<FileEntry>>.Failure(searchValidation.Error);
            }
        }

        IReadOnlyCollection<FileEntry> files;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            files = await _fileRepository.SearchByOwnerAsync(ownerId, searchTerm, folderId);
        }
        else
        {
            files = await _fileRepository.GetByOwnerAsync(ownerId, folderId);
        }

        return Result<IReadOnlyCollection<FileEntry>>.Success(files);
    }

    /// <summary>
    /// Lists files for the authenticated user with pagination.
    /// </summary>
    public async Task<Result<PagedResult<FileEntry>>> ListFilesAsync(
        Guid ownerId,
        Guid? folderId,
        string? searchTerm,
        Pagination pagination)
    {
        // Validate search term for security issues
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchValidation = FileNameValidator.ValidateSearchTerm(searchTerm);
            if (!searchValidation.Succeeded)
            {
                return Result<PagedResult<FileEntry>>.Failure(searchValidation.Error);
            }
        }

        PagedResult<FileEntry> result;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            result = await _fileRepository.SearchByOwnerAsync(ownerId, searchTerm, folderId, pagination);
        }
        else
        {
            result = await _fileRepository.GetByOwnerAsync(ownerId, folderId, pagination);
        }

        return Result<PagedResult<FileEntry>>.Success(result);
    }

    /// <summary>
    /// Deletes a file (soft delete).
    /// </summary>
    public async Task<Result> DeleteFileAsync(
        Guid fileId,
        Guid ownerId,
        string? ipAddress = null,
        string? userAgent = null)
    {
        var file = await _fileRepository.GetByIdAndOwnerAsync(fileId, ownerId);
        if (file == null)
        {
            await _auditService.LogActionAsync(
                ownerId,
                "FileDelete",
                "File",
                fileId.ToString(),
                false,
                null,
                "File not found or access denied.",
                ipAddress,
                userAgent);
            return Result.Failure("File not found or access denied.");
        }

        var deleted = await _fileRepository.DeleteAsync(fileId);
        if (!deleted)
        {
            await _auditService.LogActionAsync(
                ownerId,
                "FileDelete",
                "File",
                fileId.ToString(),
                false,
                $"File: {file.FileName}",
                "Failed to delete file.",
                ipAddress,
                userAgent);
            return Result.Failure("Failed to delete file.");
        }

        // Decrease quota (soft delete doesn't free storage immediately)
        // Quota will be updated when file is permanently deleted

        await _auditService.LogActionAsync(
            ownerId,
            "FileDelete",
            "File",
            fileId.ToString(),
            true,
            $"File: {file.FileName}, Size: {file.SizeBytes} bytes",
            null,
            ipAddress,
            userAgent);

        // Optionally delete from storage (or keep for recovery)
        // await _fileStorage.DeleteAsync(file.StoragePath);

        return Result.Success();
    }

    /// <summary>
    /// Permanently deletes a file (hard delete).
    /// </summary>
    public async Task<Result> PermanentlyDeleteFileAsync(
        Guid fileId,
        Guid ownerId,
        string? ipAddress = null,
        string? userAgent = null)
    {
        var file = await _fileRepository.GetByIdAndOwnerAsync(fileId, ownerId);
        if (file == null)
        {
            await _auditService.LogActionAsync(
                ownerId,
                "FilePermanentDelete",
                "File",
                fileId.ToString(),
                false,
                null,
                "File not found or access denied.",
                ipAddress,
                userAgent);
            return Result.Failure("File not found or access denied.");
        }

        var fileSize = file.SizeBytes;

        // Delete from storage
        try
        {
            await _fileStorage.DeleteAsync(file.StoragePath);
        }
        catch (Exception ex)
        {
            // Log but continue with database deletion
            await _auditService.LogActionAsync(
                ownerId,
                "FilePermanentDelete",
                "File",
                fileId.ToString(),
                false,
                $"File: {file.FileName}",
                $"Failed to delete from storage: {ex.Message}",
                ipAddress,
                userAgent);
        }

        var deleted = await _fileRepository.HardDeleteAsync(fileId);
        if (!deleted)
        {
            await _auditService.LogActionAsync(
                ownerId,
                "FilePermanentDelete",
                "File",
                fileId.ToString(),
                false,
                $"File: {file.FileName}",
                "Failed to permanently delete file.",
                ipAddress,
                userAgent);
            return Result.Failure("Failed to permanently delete file.");
        }

        // Decrease quota after permanent deletion
        await _quotaService.DecreaseAsync(ownerId, fileSize);

        await _auditService.LogActionAsync(
            ownerId,
            "FilePermanentDelete",
            "File",
            fileId.ToString(),
            true,
            $"File: {file.FileName}, Size: {fileSize} bytes",
            null,
            ipAddress,
            userAgent);

        return Result.Success();
    }

    /// <summary>
    /// Updates file metadata.
    /// </summary>
    public async Task<Result<FileEntry>> UpdateFileAsync(
        Guid fileId,
        Guid ownerId,
        string? fileName = null,
        string? description = null,
        Guid? folderId = null)
    {
        var file = await _fileRepository.GetByIdAndOwnerAsync(fileId, ownerId);
        if (file == null)
        {
            return Result<FileEntry>.Failure("File not found or access denied.");
        }

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            file.FileName = fileName;
            file.Extension = Path.GetExtension(fileName);
        }

        if (description != null)
        {
            file.Description = description;
        }

        if (folderId != null)
        {
            file.FolderId = folderId;
        }

        file.Touch();

        var updated = await _fileRepository.UpdateAsync(file);
        if (!updated)
        {
            return Result<FileEntry>.Failure("Failed to update file.");
        }

        return Result<FileEntry>.Success(file);
    }

    /// <summary>
    /// Gets total storage used by a user.
    /// </summary>
    public async Task<long> GetTotalStorageUsedAsync(Guid ownerId)
    {
        return await _fileRepository.GetTotalSizeByOwnerAsync(ownerId);
    }
}
