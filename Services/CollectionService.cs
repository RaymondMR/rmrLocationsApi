using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using rmrLocationsApi.Data;
using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

public class CollectionService : ICollectionService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CollectionService> _logger;

    public CollectionService(AppDbContext db, ILogger<CollectionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<LocationCollection>> GetCollectionsByUserAsync(Guid userId)
    {
        return await _db.Collections
            .AsNoTracking()
            .Where(c => c.OwnerId == userId)
            .Include(c => c.Items).ThenInclude(i => i.Location)
            .OrderByDescending(c => c.CreatedAtUtc)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<LocationCollection?> GetCollectionByIdAsync(Guid id)
    {
        return await _db.Collections
            .AsNoTracking()
            .Include(c => c.Items).ThenInclude(i => i.Location)
            .Include(c => c.Owner)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Guid?> GetOwnerIdAsync(Guid collectionId)
    {
        return await _db.Collections
            .AsNoTracking()
            .Where(c => c.Id == collectionId)
            .Select(c => (Guid?)c.OwnerId)
            .FirstOrDefaultAsync();
    }

    public async Task<LocationCollection> AddCollectionAsync(LocationCollection collection)
    {
        _db.Collections.Add(collection);
        await _db.SaveChangesAsync();
        return collection;
    }

    public async Task<bool> UpdateCollectionAsync(LocationCollection collection)
    {
        var existing = await _db.Collections.FindAsync(collection.Id);
        if (existing == null)
            return false;

        existing.Name = collection.Name;
        existing.Description = collection.Description;
        existing.Visibility = collection.Visibility;

        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency error updating collection {CollectionId}", collection.Id);
            return false;
        }
    }

    public async Task<bool> DeleteCollectionAsync(Guid id)
    {
        var existing = await _db.Collections.FindAsync(id);
        if (existing == null)
            return false;

        _db.Collections.Remove(existing);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddItemToCollectionAsync(Guid collectionId, Guid locationId, string? notes = null)
    {
        var collection = await _db.Collections.FindAsync(collectionId);
        if (collection == null)
            return false;

        var locationExists = await _db.Locations.AnyAsync(l => l.Id == locationId);
        if (!locationExists)
            return false;

        var alreadyInCollection = await _db.CollectionItems
            .AnyAsync(i => i.CollectionId == collectionId && i.LocationId == locationId);
        if (alreadyInCollection)
            return false;

        var item = new CollectionItem
        {
            CollectionId = collectionId,
            LocationId = locationId,
            Notes = notes,
            SortOrder = await _db.CollectionItems
                .Where(i => i.CollectionId == collectionId)
                .MaxAsync(i => (int?)i.SortOrder) + 1 ?? 0,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.CollectionItems.Add(item);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveItemFromCollectionAsync(Guid collectionId, Guid locationId)
    {
        var item = await _db.CollectionItems
            .FirstOrDefaultAsync(i => i.CollectionId == collectionId && i.LocationId == locationId);

        if (item == null)
            return false;

        _db.CollectionItems.Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }
}
