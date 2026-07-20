using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using rmrLocationsApi.Data;
using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

public class MediaService : IMediaService
{
    private readonly AppDbContext _db;
    private readonly ILogger<MediaService> _logger;

    public MediaService(AppDbContext db, ILogger<MediaService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<LocationMedia>> GetMediaByLocationAsync(Guid locationId)
    {
        return await _db.LocationMedia
            .AsNoTracking()
            .Where(m => m.LocationId == locationId)
            .OrderBy(m => m.SortOrder)
            .ToListAsync();
    }

    public async Task<LocationMedia?> GetMediaByIdAsync(Guid id)
    {
        return await _db.LocationMedia
            .AsNoTracking()
            .Include(m => m.Location)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<Guid?> GetLocationOwnerIdAsync(Guid mediaId)
    {
        var locationId = await _db.LocationMedia
            .AsNoTracking()
            .Where(m => m.Id == mediaId)
            .Select(m => (Guid?)m.LocationId)
            .FirstOrDefaultAsync();

        if (locationId is null)
            return null;

        return await _db.Locations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(l => l.Id == locationId)
            .Select(l => (Guid?)l.OwnerId)
            .FirstOrDefaultAsync();
    }

    public async Task<LocationMedia> AddMediaAsync(LocationMedia media)
    {
        _db.LocationMedia.Add(media);
        await _db.SaveChangesAsync();
        return media;
    }

    public async Task<bool> UpdateMediaAsync(LocationMedia media)
    {
        var existing = await _db.LocationMedia.FindAsync(media.Id);
        if (existing == null)
            return false;

        existing.Url = media.Url;
        existing.ThumbnailUrl = media.ThumbnailUrl;
        existing.Caption = media.Caption;
        existing.Type = media.Type;
        existing.IsCover = media.IsCover;
        existing.SortOrder = media.SortOrder;

        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency error updating media {MediaId}", media.Id);
            return false;
        }
    }

    public async Task<bool> DeleteMediaAsync(Guid id)
    {
        var existing = await _db.LocationMedia.FindAsync(id);
        if (existing == null)
            return false;

        _db.LocationMedia.Remove(existing);
        await _db.SaveChangesAsync();
        return true;
    }
}
