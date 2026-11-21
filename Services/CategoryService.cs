using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using rmrLocationsApi.Data;
using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(AppDbContext db, ILogger<CategoryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<Category>> GetAllCategoriesAsync()
    {
        return await _db.Categories
            .AsNoTracking()
            .Include(c => c.Children)
            .Include(c => c.ParentCategory)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<Category?> GetCategoryByIdAsync(Guid id, Guid? viewerId = null, bool viewerIsAdmin = false)
    {
        var category = await _db.Categories
            .AsNoTracking()
            .Include(c => c.Children)
            .Include(c => c.ParentCategory)
            .Include(c => c.LocationCategories).ThenInclude(lc => lc.Location)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id);

        // Solo los lugares que el visitante puede ver: un Draft / no público / archivado no se
        // filtra por aquí (antes se devolvían todos y el frontend los ocultaba).
        if (category is not null)
        {
            category.LocationCategories = category.LocationCategories
                .Where(lc => lc.Location is not null && lc.Location.IsVisibleTo(viewerId, viewerIsAdmin))
                .ToList();
        }

        return category;
    }

    public async Task<Category> AddCategoryAsync(Category category)
    {
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        return category;
    }

    public async Task<bool> UpdateCategoryAsync(Category category)
    {
        var existing = await _db.Categories.FindAsync(category.Id);
        if (existing == null)
            return false;

        existing.Name = category.Name;
        existing.Slug = category.Slug;
        existing.Description = category.Description;
        existing.IconName = category.IconName;
        existing.ColorHex = category.ColorHex;
        existing.SortOrder = category.SortOrder;
        existing.ParentCategoryId = category.ParentCategoryId;

        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency error updating category {CategoryId}", category.Id);
            return false;
        }
    }

    public async Task<bool> DeleteCategoryAsync(Guid id)
    {
        var existing = await _db.Categories.FindAsync(id);
        if (existing == null)
            return false;

        _db.Categories.Remove(existing);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> HasDependentsAsync(Guid id)
    {
        return await _db.LocationCategories.AnyAsync(lc => lc.CategoryId == id)
            || await _db.Categories.AnyAsync(c => c.ParentCategoryId == id);
    }
}
