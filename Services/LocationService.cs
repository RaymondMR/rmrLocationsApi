using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using rmrLocationsApi.Data;
using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

/// <summary>
/// Combines basic CRUD with advanced geo-spatial queries and
/// soft-delete cascade. Uses PostgreSQL + PostGIS via AppDbContext.
/// </summary>
public class LocationService : ILocationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<LocationService> _logger;

    public LocationService(AppDbContext db, ILogger<LocationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // Basic CRUD
    // ------------------------------------------------------------------

    public async Task<(List<Location> Items, int TotalCount)> GetLocationsPageAsync(
        string? name, LocationStatus? status, Guid? categoryId, IReadOnlyCollection<Guid> tagIds,
        string? sort, int page, int pageSize,
        Guid? viewerId, bool viewerIsAdmin, Guid? ownerId = null, CancellationToken ct = default)
    {
        var query = _db.Locations.AsNoTracking().AsQueryable();

        // Visibilidad: los no-admin ven publicados+públicos más sus propios lugares.
        query = query.VisibleTo(viewerId, viewerIsAdmin);

        // Lugares de un dueño concreto (p. ej. «Mis lugares» del perfil). Va DESPUÉS de la visibilidad: pedir
        // los lugares de otra persona solo devuelve lo que cualquiera podría ver de ella.
        if (ownerId is Guid oid)
            query = query.Where(l => l.OwnerId == oid);

        if (status is not null)
            query = query.Where(l => l.Status == status);

        if (categoryId is Guid cid)
            query = query.Where(l => l.LocationCategories.Any(lc => lc.CategoryId == cid));

        foreach (var tagId in tagIds)
            query = query.Where(l => l.LocationTags.Any(lt => lt.TagId == tagId));

        if (!string.IsNullOrWhiteSpace(name))
        {
            var pattern = ToIlikePattern(name);
            query = query.Where(l => EF.Functions.ILike(l.Name, pattern));
        }

        var totalCount = await query.CountAsync(ct);

        query = sort?.ToLowerInvariant() switch
        {
            "rating" => query.OrderByDescending(l => l.AverageRating)
                             .ThenByDescending(l => l.ReviewCount)
                             .ThenBy(l => l.Name),
            "recent" => query.OrderByDescending(l => l.CreatedAtUtc),
            _ => query.OrderBy(l => l.Name),
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(l => l.LocationCategories).ThenInclude(lc => lc.Category)
            .Include(l => l.LocationTags).ThenInclude(lt => lt.Tag)
            .AsSplitQuery()
            .ToListAsync(ct);

        return (items, totalCount);
    }

    /// <summary>Patrón ILIKE con comodines escapados (aprovecha el índice GIN trigram de Name).</summary>
    private static string ToIlikePattern(string term)
        => "%" + term.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";

    public async Task<Location?> GetLocationByIdAsync(Guid id, Guid? viewerId = null, bool viewerIsAdmin = false)
    {
        return await _db.Locations
            .AsNoTracking()
            .VisibleTo(viewerId, viewerIsAdmin) // un Draft / no público / archivado no se revela a terceros
            .Include(l => l.LocationCategories).ThenInclude(lc => lc.Category)
            .Include(l => l.LocationTags).ThenInclude(lt => lt.Tag)
            .Include(l => l.Media)
            .Include(l => l.OpeningHours)
            .Include(l => l.Reviews)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == id);
    }

    public Task<bool> IsVisibleAsync(Guid locationId, Guid? viewerId = null, bool viewerIsAdmin = false, CancellationToken ct = default)
        => _db.Locations
            .AsNoTracking()
            .VisibleTo(viewerId, viewerIsAdmin)
            .AnyAsync(l => l.Id == locationId, ct);

    public async Task<Location> AddLocationAsync(Location location)
    {
        _db.Locations.Add(location);
        await _db.SaveChangesAsync();
        return location;
    }

    public async Task<bool> UpdateLocationAsync(Location location)
    {
        var existing = await _db.Locations.FindAsync(location.Id);
        if (existing == null)
            return false;

        existing.Name = location.Name;
        existing.Slug = location.Slug;
        existing.Description = location.Description;
        existing.WebsiteUrl = location.WebsiteUrl;
        existing.PhoneNumber = location.PhoneNumber;
        existing.Status = location.Status;
        existing.IsPublic = location.IsPublic;
        existing.Address = location.Address;

        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency error updating location {LocationId}", location.Id);
            return false;
        }
    }

    public async Task<bool> DeleteLocationAsync(Guid id)
    {
        var existing = await _db.Locations.FindAsync(id);
        if (existing == null)
            return false;

        _db.Locations.Remove(existing);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// OwnerId of the location, including soft-deleted ones (needed to authorize restore).
    /// Null when the location does not exist.
    /// </summary>
    public async Task<Guid?> GetOwnerIdAsync(Guid locationId, CancellationToken ct = default)
    {
        return await _db.Locations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(l => l.Id == locationId)
            .Select(l => (Guid?)l.OwnerId)
            .FirstOrDefaultAsync(ct);
    }

    // ------------------------------------------------------------------
    // Soft delete with logical cascade
    // ------------------------------------------------------------------

    public async Task<bool> SoftDeleteLocationAsync(Guid locationId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var affected = await _db.Locations
            .Where(l => l.Id == locationId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.IsDeleted, true)
                .SetProperty(l => l.DeletedAtUtc, now), ct);

        if (affected == 0)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        // Children with soft delete: mark
        await _db.LocationMedia
            .Where(m => m.LocationId == locationId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.IsDeleted, true)
                .SetProperty(m => m.DeletedAtUtc, now), ct);

        await _db.Reviews
            .Where(r => r.LocationId == locationId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.IsDeleted, true)
                .SetProperty(r => r.DeletedAtUtc, now), ct);

        // Join tables and child data without historical value: physical delete
        await _db.OpeningHours.Where(h => h.LocationId == locationId).ExecuteDeleteAsync(ct);
        await _db.UserFavoriteLocations.Where(f => f.LocationId == locationId).ExecuteDeleteAsync(ct);
        await _db.CollectionItems.Where(i => i.LocationId == locationId).ExecuteDeleteAsync(ct);
        await _db.LocationCategories.Where(c => c.LocationId == locationId).ExecuteDeleteAsync(ct);

        // When detaching tags, adjust the denormalized counter
        var tagIds = await _db.LocationTags
            .Where(t => t.LocationId == locationId)
            .Select(t => t.TagId)
            .ToListAsync(ct);

        await _db.LocationTags.Where(t => t.LocationId == locationId).ExecuteDeleteAsync(ct);

        if (tagIds.Count > 0)
        {
            await _db.Tags
                .Where(t => tagIds.Contains(t.Id) && t.UsageCount > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsageCount, t => t.UsageCount - 1), ct);
        }

        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> RestoreLocationAsync(Guid locationId, CancellationToken ct = default)
    {
        var affected = await _db.Locations
            .IgnoreQueryFilters()
            .Where(l => l.Id == locationId && l.IsDeleted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.IsDeleted, false)
                .SetProperty(l => l.DeletedAtUtc, (DateTimeOffset?)null), ct);

        return affected > 0;
    }

    // ------------------------------------------------------------------
    // Geo-spatial queries (PostGIS: ST_DWithin / KNN)
    // ------------------------------------------------------------------

    /// <summary>Locations within a radius, ordered by proximity. Distances in METERS.</summary>
    public async Task<List<(Location Location, double Meters)>> NearbyAsync(
        double latitude, double longitude, double radiusMeters, int take = 20,
        CancellationToken ct = default)
    {
        var origin = Geo.Point(latitude, longitude);

        var rows = await _db.Locations
            .Where(l => l.Status == LocationStatus.Published && l.IsPublic)
            .Where(l => l.Coordinates.IsWithinDistance(origin, radiusMeters))
            .OrderBy(l => l.Coordinates.Distance(origin))
            .Take(take)
            .Select(l => new { Location = l, Meters = l.Coordinates.Distance(origin) })
            .ToListAsync(ct);

        return rows.Select(r => (r.Location, r.Meters)).ToList();
    }

    /// <summary>Combined filter: proximity + categories + tags (AND between tags).</summary>
    public Task<List<Location>> SearchAsync(
        double latitude, double longitude, double radiusMeters,
        IReadOnlyCollection<Guid> categoryIds,
        IReadOnlyCollection<Guid> tagIds,
        string? name = null,
        CancellationToken ct = default)
    {
        var origin = Geo.Point(latitude, longitude);

        var query = _db.Locations
            .Where(l => l.Status == LocationStatus.Published && l.IsPublic)
            .Where(l => l.Coordinates.IsWithinDistance(origin, radiusMeters));

        if (categoryIds.Count > 0)
            query = query.Where(l => l.LocationCategories.Any(lc => categoryIds.Contains(lc.CategoryId)));

        foreach (var tagId in tagIds)
            query = query.Where(l => l.LocationTags.Any(lt => lt.TagId == tagId));

        if (!string.IsNullOrWhiteSpace(name))
        {
            var pattern = ToIlikePattern(name);
            query = query.Where(l => EF.Functions.ILike(l.Name, pattern));
        }

        return query
            .Include(l => l.LocationCategories).ThenInclude(lc => lc.Category)
            .Include(l => l.LocationTags).ThenInclude(lt => lt.Tag)
            .OrderBy(l => l.Coordinates.Distance(origin))
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    /// <summary>Recalculate denormalized rating after creating/editing/deleting a review.</summary>
    public Task RefreshRatingAsync(Guid locationId, CancellationToken ct = default)
        => _db.Database.ExecuteSqlAsync($"""
            UPDATE locations l
            SET "AverageRating" = COALESCE(s.avg_rating, 0),
                "ReviewCount"   = COALESCE(s.cnt, 0)
            FROM (
                SELECT AVG("Rating")::float8 AS avg_rating, COUNT(*) AS cnt
                FROM reviews
                WHERE "LocationId" = {locationId} AND NOT "IsDeleted"
            ) s
            WHERE l."Id" = {locationId}
            """, ct);
}
