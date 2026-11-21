using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

public interface ICategoryService
{
    Task<List<Category>> GetAllCategoriesAsync();
    // Los lugares anidados (locationCategories[].location) se filtran con LocationVisibility.
    Task<Category?> GetCategoryByIdAsync(Guid id, Guid? viewerId = null, bool viewerIsAdmin = false);
    Task<Category> AddCategoryAsync(Category category);
    Task<bool> UpdateCategoryAsync(Category category);
    Task<bool> DeleteCategoryAsync(Guid id);

    // True si la categoría tiene lugares asociados o subcategorías vivas
    // (el soft delete nunca dispara el FK Restrict, así que se valida aquí).
    Task<bool> HasDependentsAsync(Guid id);
}
