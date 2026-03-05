using Microsoft.EntityFrameworkCore;
using MiniDrive.Common;
using MiniDrive.Files.Entities;

namespace MiniDrive.Files.Repositories;

/// <summary>
/// Repository for file entry data access.
/// </summary>
public class FileRepository
{
    private readonly FileDbContext _context;

    public FileRepository(FileDbContext context)
    {
        _context = context;
    }

    public async Task<FileEntry?> GetByIdAsync(Guid id)
    {
        return await _context.Files.FindAsync(id);
    }

    public async Task<FileEntry?> GetByIdAndOwnerAsync(Guid id, Guid ownerId)
    {
        return await _context.Files
            .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == ownerId && !f.IsDeleted);
    }

    public async Task<IReadOnlyCollection<FileEntry>> GetByOwnerAsync(Guid ownerId, Guid? folderId = null)
    {
        var query = _context.Files
            .Where(f => f.OwnerId == ownerId && !f.IsDeleted);

        if (folderId == null)
        {
            query = query.Where(f => f.FolderId == null);
        }
        else
        {
            query = query.Where(f => f.FolderId == folderId);
        }

        return await query
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<PagedResult<FileEntry>> GetByOwnerAsync(
        Guid ownerId,
        Guid? folderId,
        Pagination pagination)
    {
        var query = _context.Files
            .Where(f => f.OwnerId == ownerId && !f.IsDeleted);

        if (folderId == null)
        {
            query = query.Where(f => f.FolderId == null);
        }
        else
        {
            query = query.Where(f => f.FolderId == folderId);
        }

        var totalCount = await query.LongCountAsync();

        var items = await query
            .OrderByDescending(f => f.CreatedAtUtc)
            .Skip(pagination.Skip)
            .Take(pagination.Take)
            .ToListAsync();

        return new PagedResult<FileEntry>(items, pagination.PageNumber, pagination.PageSize, totalCount);
    }

    public async Task<IReadOnlyCollection<FileEntry>> SearchByOwnerAsync(
        Guid ownerId,
        string? searchTerm,
        Guid? folderId = null)
    {
        var query = _context.Files
            .Where(f => f.OwnerId == ownerId && !f.IsDeleted);

        if (folderId != null)
        {
            query = query.Where(f => f.FolderId == folderId);
        }
        else
        {
            query = query.Where(f => f.FolderId == null);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLowerInvariant();
            query = query.Where(f =>
                EF.Functions.Like(f.FileName.ToLower(), $"%{term}%") ||
                (f.Description != null && EF.Functions.Like(f.Description.ToLower(), $"%{term}%")));
        }

        return await query
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<PagedResult<FileEntry>> SearchByOwnerAsync(
        Guid ownerId,
        string? searchTerm,
        Guid? folderId,
        Pagination pagination)
    {
        var query = _context.Files
            .Where(f => f.OwnerId == ownerId && !f.IsDeleted);

        if (folderId != null)
        {
            query = query.Where(f => f.FolderId == folderId);
        }
        else
        {
            query = query.Where(f => f.FolderId == null);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLowerInvariant();
            query = query.Where(f =>
                EF.Functions.Like(f.FileName.ToLower(), $"%{term}%") ||
                (f.Description != null && EF.Functions.Like(f.Description.ToLower(), $"%{term}%")));
        }

        var totalCount = await query.LongCountAsync();

        var items = await query
            .OrderByDescending(f => f.CreatedAtUtc)
            .Skip(pagination.Skip)
            .Take(pagination.Take)
            .ToListAsync();

        return new PagedResult<FileEntry>(items, pagination.PageNumber, pagination.PageSize, totalCount);
    }

    public async Task<FileEntry> CreateAsync(FileEntry file)
    {
        _context.Files.Add(file);
        await _context.SaveChangesAsync();
        return file;
    }

    public async Task<bool> UpdateAsync(FileEntry file)
    {
        var existing = await _context.Files.FindAsync(file.Id);
        if (existing == null)
        {
            return false;
        }

        file.Touch();
        _context.Entry(existing).CurrentValues.SetValues(file);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var file = await _context.Files.FindAsync(id);
        if (file == null)
        {
            return false;
        }

        file.IsDeleted = true;
        file.DeletedAtUtc = DateTime.UtcNow;
        file.Touch();
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> HardDeleteAsync(Guid id)
    {
        var file = await _context.Files.FindAsync(id);
        if (file == null)
        {
            return false;
        }

        _context.Files.Remove(file);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<long> GetTotalSizeByOwnerAsync(Guid ownerId)
    {
        return await _context.Files
            .Where(f => f.OwnerId == ownerId && !f.IsDeleted)
            .SumAsync(f => f.SizeBytes);
    }
}
