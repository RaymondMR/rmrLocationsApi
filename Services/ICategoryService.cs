using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

public interface ICategoryService
{
    Task<List<Category>> GetAllCategoriesAsync();
    Task<Category?> GetCategoryByIdAsync(Guid id);
    Task<Category> AddCategoryAsync(Category category);
    Task<bool> UpdateCategoryAsync(Category category);
    Task<bool> DeleteCategoryAsync(Guid id);

    // True si la categoría tiene lugares asociados o subcategorías vivas
    // (el soft delete nunca dispara el FK Restrict, así que se valida aquí).
    Task<bool> HasDependentsAsync(Guid id);
}
