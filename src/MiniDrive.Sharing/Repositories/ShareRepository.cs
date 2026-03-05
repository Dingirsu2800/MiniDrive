using Microsoft.EntityFrameworkCore;
using MiniDrive.Common;
using MiniDrive.Sharing.Entities;

namespace MiniDrive.Sharing.Repositories;

/// <summary>
/// Repository for managing Share entities.
/// </summary>
public class ShareRepository
{
    private readonly SharingDbContext _context;

    public ShareRepository(SharingDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates a new share.
    /// </summary>
    public async Task<Share> CreateAsync(Share share)
    {
        _context.Shares.Add(share);
        await _context.SaveChangesAsync();
        return share;
    }

    /// <summary>
    /// Gets a share by ID.
    /// </summary>
    public async Task<Share?> GetByIdAsync(Guid id)
    {
        return await _context.Shares.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
    }

    /// <summary>
    /// Gets a share by public share token.
    /// </summary>
    public async Task<Share?> GetByShareTokenAsync(string token)
    {
        return await _context.Shares.FirstOrDefaultAsync(s => s.ShareToken == token && !s.IsDeleted && s.IsActive);
    }

    /// <summary>
    /// Gets shares for a specific resource.
    /// </summary>
    public async Task<IReadOnlyCollection<Share>> GetByResourceAsync(Guid resourceId, string resourceType)
    {
        return await _context.Shares
            .Where(s => s.ResourceId == resourceId && s.ResourceType == resourceType && !s.IsDeleted)
            .ToListAsync();
    }

    /// <summary>
    /// Gets shares created by a user.
    /// </summary>
    public async Task<IReadOnlyCollection<Share>> GetByOwnerAsync(Guid ownerId)
    {
        return await _context.Shares
            .Where(s => s.OwnerId == ownerId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync();
    }

    /// <summary>
    /// Gets shares created by a user with pagination.
    /// </summary>
    public async Task<PagedResult<Share>> GetByOwnerAsync(Guid ownerId, Pagination pagination)
    {
        var query = _context.Shares
            .Where(s => s.OwnerId == ownerId && !s.IsDeleted);

        var totalCount = await query.LongCountAsync();

        var items = await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip(pagination.Skip)
            .Take(pagination.Take)
            .ToListAsync();

        return new PagedResult<Share>(items, pagination.PageNumber, pagination.PageSize, totalCount);
    }

    /// <summary>
    /// Gets shares with a specific user.
    /// </summary>
    public async Task<IReadOnlyCollection<Share>> GetBySharedWithUserAsync(Guid userId)
    {
        return await _context.Shares
            .Where(s => s.SharedWithUserId == userId && !s.IsDeleted && s.IsActive)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync();
    }

    /// <summary>
    /// Gets shares with a specific user with pagination.
    /// </summary>
    public async Task<PagedResult<Share>> GetBySharedWithUserAsync(Guid userId, Pagination pagination)
    {
        var query = _context.Shares
            .Where(s => s.SharedWithUserId == userId && !s.IsDeleted && s.IsActive);

        var totalCount = await query.LongCountAsync();

        var items = await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip(pagination.Skip)
            .Take(pagination.Take)
            .ToListAsync();

        return new PagedResult<Share>(items, pagination.PageNumber, pagination.PageSize, totalCount);
    }

    /// <summary>
    /// Gets public shares for a resource.
    /// </summary>
    public async Task<IReadOnlyCollection<Share>> GetPublicSharesAsync(Guid resourceId, string resourceType)
    {
        return await _context.Shares
            .Where(s => s.ResourceId == resourceId && s.ResourceType == resourceType && 
                        s.IsPublicShare && !s.IsDeleted && s.IsActive)
            .ToListAsync();
    }

    /// <summary>
    /// Updates a share.
    /// </summary>
    public async Task<bool> UpdateAsync(Share share)
    {
        share.Touch();
        _context.Shares.Update(share);
        var result = await _context.SaveChangesAsync();
        return result > 0;
    }

    /// <summary>
    /// Soft deletes a share.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id)
    {
        var share = await _context.Shares.FirstOrDefaultAsync(s => s.Id == id);
        if (share == null)
            return false;

        share.IsDeleted = true;
        share.Touch();
        _context.Shares.Update(share);
        var result = await _context.SaveChangesAsync();
        return result > 0;
    }

    /// <summary>
    /// Checks if a share exists and is accessible.
    /// </summary>
    public async Task<bool> ExistsAsync(Guid id, Guid ownerId)
    {
        return await _context.Shares.AnyAsync(s => s.Id == id && s.OwnerId == ownerId && !s.IsDeleted);
    }

    /// <summary>
    /// Checks if a resource is shared with a specific user.
    /// </summary>
    public async Task<Share?> GetShareWithUserAsync(Guid resourceId, string resourceType, Guid userId)
    {
        return await _context.Shares.FirstOrDefaultAsync(s =>
            s.ResourceId == resourceId &&
            s.ResourceType == resourceType &&
            s.SharedWithUserId == userId &&
            !s.IsDeleted &&
            s.IsActive);
    }

    /// <summary>
    /// Gets all shares for resources with pagination.
    /// </summary>
    public async Task<(IReadOnlyCollection<Share> Items, int Total)> GetPaginatedAsync(
        Guid ownerId,
        int pageNumber = 1,
        int pageSize = 10)
    {
        var query = _context.Shares.Where(s => s.OwnerId == ownerId && !s.IsDeleted);
        var total = await query.CountAsync();
        
        var items = await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
