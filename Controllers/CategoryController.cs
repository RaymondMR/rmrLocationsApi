using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using rmrLocationsApi.Extensions;
using rmrLocationsApi.Handlers;
using rmrLocationsApi.Models;
using rmrLocationsApi.Services;

namespace rmrLocationsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = UserRoles.Admin)] // gestión solo Admin; las lecturas llevan [AllowAnonymous]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;
    private readonly ILogger<CategoryController> _logger;

    public CategoryController(ICategoryService categoryService, ILogger<CategoryController> logger)
    {
        _categoryService = categoryService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<Category>>> GetAllCategories()
    {
        try
        {
            var categories = await _categoryService.GetAllCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all categories");
            return StatusCode(500, "An error occurred while retrieving categories.");
        }
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<Category>> GetCategoryById(Guid id)
    {
        try
        {
            var category = await _categoryService.GetCategoryByIdAsync(id, User.GetUserId(), User.IsAdmin());
            if (category == null)
                return NotFound();
            return Ok(category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting category {CategoryId}", id);
            return StatusCode(500, "An error occurred while retrieving the category.");
        }
    }

    [HttpPost]
    public async Task<ActionResult<Category>> CreateCategory([FromBody] Category category)
    {
        try
        {
            if (category == null)
                return BadRequest("Category data is required.");

            var created = await _categoryService.AddCategoryAsync(category);
            return CreatedAtAction(nameof(GetCategoryById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            var mapped = DbErrorMapper.ToActionResult(ex);
            if (mapped is not null)
                return mapped;

            _logger.LogError(ex, "Error creating category");
            return StatusCode(500, "An error occurred while creating the category.");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] Category category)
    {
        try
        {
            if (category == null)
                return BadRequest("Category data is required.");
            if (id != category.Id)
                category.Id = id;

            var updated = await _categoryService.UpdateCategoryAsync(category);
            if (!updated)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            var mapped = DbErrorMapper.ToActionResult(ex);
            if (mapped is not null)
                return mapped;

            _logger.LogError(ex, "Error updating category {CategoryId}", id);
            return StatusCode(500, "An error occurred while updating the category.");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        try
        {
            // El DELETE es soft (interceptor de SaveChanges) y nunca dispara el FK
            // Restrict, así que la regla "no borrar categorías en uso" se valida aquí.
            if (await _categoryService.HasDependentsAsync(id))
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = "The category has locations or subcategories and cannot be deleted."
                });
            }

            var deleted = await _categoryService.DeleteCategoryAsync(id);
            if (!deleted)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            var mapped = DbErrorMapper.ToActionResult(ex);
            if (mapped is not null)
                return mapped;

            _logger.LogError(ex, "Error deleting category {CategoryId}", id);
            return StatusCode(500, "An error occurred while deleting the category.");
        }
    }
}
