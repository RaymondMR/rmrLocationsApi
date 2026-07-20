using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

public interface ICollectionService
{
    Task<List<LocationCollection>> GetCollectionsByUserAsync(Guid userId);
    Task<LocationCollection?> GetCollectionByIdAsync(Guid id);
    Task<LocationCollection> AddCollectionAsync(LocationCollection collection);
    Task<bool> UpdateCollectionAsync(LocationCollection collection);
    Task<bool> DeleteCollectionAsync(Guid id);
    Task<bool> AddItemToCollectionAsync(Guid collectionId, Guid locationId, string? notes = null);
    Task<bool> RemoveItemFromCollectionAsync(Guid collectionId, Guid locationId);

    // Authorization: owner lookup
    Task<Guid?> GetOwnerIdAsync(Guid collectionId);
}
