using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

public interface ILocationService
{
    // Basic CRUD
    Task<List<Location>> GetAllLocationsAsync();
    Task<Location?> GetLocationByIdAsync(Guid id);
    Task<Location> AddLocationAsync(Location location);
    Task<bool> UpdateLocationAsync(Location location);
    Task<bool> DeleteLocationAsync(Guid id);

    // Soft delete cascade (logical cascade for auditable children)
    Task<bool> SoftDeleteLocationAsync(Guid locationId, CancellationToken ct = default);

    // Restore
    Task<bool> RestoreLocationAsync(Guid locationId, CancellationToken ct = default);

    // Geo-spatial
    Task<List<(Location Location, double Meters)>> NearbyAsync(
        double latitude, double longitude, double radiusMeters, int take = 20,
        CancellationToken ct = default);

    // Combined search: geo + categories + tags (AND logic for tags)
    Task<List<Location>> SearchAsync(
        double latitude, double longitude, double radiusMeters,
        IReadOnlyCollection<Guid> categoryIds,
        IReadOnlyCollection<Guid> tagIds,
        CancellationToken ct = default);

    // Denormalized rating refresh
    Task RefreshRatingAsync(Guid locationId, CancellationToken ct = default);
}
