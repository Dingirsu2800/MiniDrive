using Microsoft.EntityFrameworkCore;
using MiniDrive.Common;
using MiniDrive.Folders.Entities;

namespace MiniDrive.Folders.Repositories;

/// <summary>
/// Repository for folder data access.
/// </summary>
public class FolderRepository
{
    private readonly FolderDbContext _context;

    public FolderRepository(FolderDbContext context)
    {
        _context = context;
    }

    public async Task<Folder?> GetByIdAsync(Guid id)
    {
        return await _context.Folders.FindAsync(id);
    }

    public async Task<Folder?> GetByIdAndOwnerAsync(Guid id, Guid ownerId)
    {
        return await _context.Folders
            .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == ownerId && !f.IsDeleted);
    }

    public async Task<Folder?> GetByNameAndParentAsync(string name, Guid ownerId, Guid? parentFolderId)
    {
        return await _context.Folders
            .FirstOrDefaultAsync(f =>
                f.OwnerId == ownerId &&
                !f.IsDeleted &&
                f.Name == name &&
                f.ParentFolderId == parentFolderId);
    }

    public async Task<IReadOnlyCollection<Folder>> GetByOwnerAsync(Guid ownerId, Guid? parentFolderId = null)
    {
        var query = _context.Folders
            .Where(f => f.OwnerId == ownerId && !f.IsDeleted);

        if (parentFolderId == null)
        {
            query = query.Where(f => f.ParentFolderId == null);
        }
        else
        {
            query = query.Where(f => f.ParentFolderId == parentFolderId);
        }

        return await query
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    public async Task<PagedResult<Folder>> GetByOwnerAsync(
        Guid ownerId,
        Guid? parentFolderId,
        Pagination pagination)
    {
        var query = _context.Folders
            .Where(f => f.OwnerId == ownerId && !f.IsDeleted);

        if (parentFolderId == null)
        {
            query = query.Where(f => f.ParentFolderId == null);
        }
        else
        {
            query = query.Where(f => f.ParentFolderId == parentFolderId);
        }

        var totalCount = await query.LongCountAsync();

        var items = await query
            .OrderBy(f => f.Name)
            .Skip(pagination.Skip)
            .Take(pagination.Take)
            .ToListAsync();

        return new PagedResult<Folder>(items, pagination.PageNumber, pagination.PageSize, totalCount);
    }

    public async Task<IReadOnlyCollection<Folder>> SearchByOwnerAsync(
        Guid ownerId,
        string? searchTerm,
        Guid? parentFolderId = null)
    {
        var query = _context.Folders
            .Where(f => f.OwnerId == ownerId && !f.IsDeleted);

        if (parentFolderId != null)
        {
            query = query.Where(f => f.ParentFolderId == parentFolderId);
        }
        else
        {
            query = query.Where(f => f.ParentFolderId == null);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLowerInvariant();
            query = query.Where(f =>
                EF.Functions.Like(f.Name.ToLower(), $"%{term}%") ||
                (f.Description != null && EF.Functions.Like(f.Description.ToLower(), $"%{term}%")));
        }

        return await query
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    public async Task<PagedResult<Folder>> SearchByOwnerAsync(
        Guid ownerId,
        string? searchTerm,
        Guid? parentFolderId,
        Pagination pagination)
    {
        var query = _context.Folders
            .Where(f => f.OwnerId == ownerId && !f.IsDeleted);

        if (parentFolderId != null)
        {
            query = query.Where(f => f.ParentFolderId == parentFolderId);
        }
        else
        {
            query = query.Where(f => f.ParentFolderId == null);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLowerInvariant();
            query = query.Where(f =>
                EF.Functions.Like(f.Name.ToLower(), $"%{term}%") ||
                (f.Description != null && EF.Functions.Like(f.Description.ToLower(), $"%{term}%")));
        }

        var totalCount = await query.LongCountAsync();

        var items = await query
            .OrderBy(f => f.Name)
            .Skip(pagination.Skip)
            .Take(pagination.Take)
            .ToListAsync();

        return new PagedResult<Folder>(items, pagination.PageNumber, pagination.PageSize, totalCount);
    }

    public async Task<Folder> CreateAsync(Folder folder)
    {
        _context.Folders.Add(folder);
        await _context.SaveChangesAsync();
        return folder;
    }

    public async Task<bool> UpdateAsync(Folder folder)
    {
        var existing = await _context.Folders.FindAsync(folder.Id);
        if (existing == null)
        {
            return false;
        }

        folder.Touch();
        _context.Entry(existing).CurrentValues.SetValues(folder);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var folder = await _context.Folders.FindAsync(id);
        if (folder == null)
        {
            return false;
        }

        folder.IsDeleted = true;
        folder.DeletedAtUtc = DateTime.UtcNow;
        folder.Touch();
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> HardDeleteAsync(Guid id)
    {
        var folder = await _context.Folders.FindAsync(id);
        if (folder == null)
        {
            return false;
        }

        _context.Folders.Remove(folder);
        await _context.SaveChangesAsync();
        return true;
    }
}

