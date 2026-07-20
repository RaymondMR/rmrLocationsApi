using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

public interface ILocationService
{
    // Basic CRUD
    // Listado paginado con búsqueda por nombre (ILIKE + índice trigram), filtros y
    // visibilidad por rol (anónimo: publicados; autenticado: + propios; Admin: todo)
    Task<(List<Location> Items, int TotalCount)> GetLocationsPageAsync(
        string? name, LocationStatus? status, Guid? categoryId, IReadOnlyCollection<Guid> tagIds,
        string? sort, int page, int pageSize,
        Guid? viewerId, bool viewerIsAdmin, CancellationToken ct = default);
    Task<Location?> GetLocationByIdAsync(Guid id);
    Task<Location> AddLocationAsync(Location location);
    Task<bool> UpdateLocationAsync(Location location);
    Task<bool> DeleteLocationAsync(Guid id);

    // Authorization: owner lookup (includes soft-deleted, so restore can be authorized)
    Task<Guid?> GetOwnerIdAsync(Guid locationId, CancellationToken ct = default);

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
        string? name = null,
        CancellationToken ct = default);

    // Denormalized rating refresh
    Task RefreshRatingAsync(Guid locationId, CancellationToken ct = default);
}
