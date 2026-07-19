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

    public async Task<List<Tag>> GetAllTagsAsync()
    {
        return await _db.Tags
            .AsNoTracking()
            .Include(t => t.LocationTags)
            .ToListAsync();
    }

    public async Task<Tag?> GetTagByIdAsync(Guid id)
    {
        return await _db.Tags
            .AsNoTracking()
            .Include(t => t.LocationTags).ThenInclude(lt => lt.Location)
            .FirstOrDefaultAsync(t => t.Id == id);
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
