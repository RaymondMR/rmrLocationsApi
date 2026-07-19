using Microsoft.AspNetCore.Mvc;
using rmrLocationsApi.Models;
using rmrLocationsApi.Services;

namespace rmrLocationsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CollectionController : ControllerBase
{
    private readonly ICollectionService _collectionService;
    private readonly ILogger<CollectionController> _logger;

    public CollectionController(ICollectionService collectionService, ILogger<CollectionController> logger)
    {
        _collectionService = collectionService;
        _logger = logger;
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<List<LocationCollection>>> GetCollectionsByUser(Guid userId)
    {
        try
        {
            var collections = await _collectionService.GetCollectionsByUserAsync(userId);
            return Ok(collections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting collections for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving collections.");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LocationCollection>> GetCollectionById(Guid id)
    {
        try
        {
            var collection = await _collectionService.GetCollectionByIdAsync(id);
            if (collection == null)
                return NotFound();
            return Ok(collection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting collection {CollectionId}", id);
            return StatusCode(500, "An error occurred while retrieving the collection.");
        }
    }

    [HttpPost]
    public async Task<ActionResult<LocationCollection>> CreateCollection([FromBody] LocationCollection collection)
    {
        try
        {
            if (collection == null)
                return BadRequest("Collection data is required.");

            var created = await _collectionService.AddCollectionAsync(collection);
            return CreatedAtAction(nameof(GetCollectionById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating collection");
            return StatusCode(500, "An error occurred while creating the collection.");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCollection(Guid id, [FromBody] LocationCollection collection)
    {
        try
        {
            if (collection == null)
                return BadRequest("Collection data is required.");
            if (id != collection.Id)
                collection.Id = id;

            var updated = await _collectionService.UpdateCollectionAsync(collection);
            if (!updated)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating collection {CollectionId}", id);
            return StatusCode(500, "An error occurred while updating the collection.");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCollection(Guid id)
    {
        try
        {
            var deleted = await _collectionService.DeleteCollectionAsync(id);
            if (!deleted)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting collection {CollectionId}", id);
            return StatusCode(500, "An error occurred while deleting the collection.");
        }
    }

    [HttpPost("{collectionId:guid}/items")]
    public async Task<IActionResult> AddItemToCollection(Guid collectionId, [FromBody] AddCollectionItemRequest request)
    {
        try
        {
            var added = await _collectionService.AddItemToCollectionAsync(
                collectionId, request.LocationId, request.Notes);
            if (!added)
                return BadRequest("Could not add item. Collection or location not found, or item already in collection.");
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to collection {CollectionId}", collectionId);
            return StatusCode(500, "An error occurred while adding the item.");
        }
    }

    [HttpDelete("{collectionId:guid}/items/{locationId:guid}")]
    public async Task<IActionResult> RemoveItemFromCollection(Guid collectionId, Guid locationId)
    {
        try
        {
            var removed = await _collectionService.RemoveItemFromCollectionAsync(collectionId, locationId);
            if (!removed)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item from collection {CollectionId}", collectionId);
            return StatusCode(500, "An error occurred while removing the item.");
        }
    }
}

// Simple DTO for adding items
public record AddCollectionItemRequest(Guid LocationId, string? Notes = null);
