using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

public interface ICollectionService
{
    // Los lugares de los items se filtran con LocationVisibility (según el visitante).
    Task<List<LocationCollection>> GetCollectionsByUserAsync(Guid userId, Guid? viewerId = null, bool viewerIsAdmin = false);
    Task<LocationCollection?> GetCollectionByIdAsync(Guid id, Guid? viewerId = null, bool viewerIsAdmin = false);
    Task<LocationCollection> AddCollectionAsync(LocationCollection collection);
    Task<bool> UpdateCollectionAsync(LocationCollection collection);
    Task<bool> DeleteCollectionAsync(Guid id);
    // Solo se puede guardar un lugar que el visitante puede ver (un Draft ajeno se trata como inexistente).
    Task<bool> AddItemToCollectionAsync(Guid collectionId, Guid locationId, string? notes = null, Guid? viewerId = null, bool viewerIsAdmin = false);
    Task<bool> RemoveItemFromCollectionAsync(Guid collectionId, Guid locationId);

    // Authorization: owner lookup
    Task<Guid?> GetOwnerIdAsync(Guid collectionId);
}
