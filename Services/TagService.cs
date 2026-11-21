using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using rmrLocationsApi.Data;
using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

public class TagService : ITagService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TagService> _logger;

    public TagService(AppDbContext db, ILogger<TagService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<Tag>> GetAllTagsAsync(Guid? viewerId = null, bool viewerIsAdmin = false)
    {
        var tags = await _db.Tags
            .AsNoTracking()
            .ToListAsync();

        // Solo las filas de unión con lugares que el visitante puede ver: así la nube de tags no cuenta
        // lugares Draft y no se filtran sus ids. (Subconsulta sobre una consulta capturada fuera del lambda.)
        var visible = _db.Locations.VisibleTo(viewerId, viewerIsAdmin);
        var links = await _db.LocationTags
            .AsNoTracking()
            .Where(lt => visible.Any(l => l.Id == lt.LocationId))
            .ToListAsync();

        foreach (var tag in tags)
            tag.LocationTags = links.Where(lt => lt.TagId == tag.Id).ToList();

        return tags;
    }

    public async Task<Tag?> GetTagByIdAsync(Guid id, Guid? viewerId = null, bool viewerIsAdmin = false)
    {
        var tag = await _db.Tags
            .AsNoTracking()
            .Include(t => t.LocationTags).ThenInclude(lt => lt.Location)
            .FirstOrDefaultAsync(t => t.Id == id);

        // Un Draft / no público / archivado no se revela a terceros por esta ruta.
        if (tag is not null)
        {
            tag.LocationTags = tag.LocationTags
                .Where(lt => lt.Location is not null && lt.Location.IsVisibleTo(viewerId, viewerIsAdmin))
                .ToList();
        }

        return tag;
    }

    public async Task<Tag> AddTagAsync(Tag tag)
    {
        _db.Tags.Add(tag);
        await _db.SaveChangesAsync();
        return tag;
    }

    public async Task<bool> UpdateTagAsync(Tag tag)
    {
        var existing = await _db.Tags.FindAsync(tag.Id);
        if (existing == null)
            return false;

        existing.Name = tag.Name;
        existing.Slug = tag.Slug;
        existing.Description = tag.Description;

        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency error updating tag {TagId}", tag.Id);
            return false;
        }
    }

    public async Task<bool> DeleteTagAsync(Guid id)
    {
        var existing = await _db.Tags.FindAsync(id);
        if (existing == null)
            return false;

        _db.Tags.Remove(existing);
        await _db.SaveChangesAsync();
        return true;
    }
}
