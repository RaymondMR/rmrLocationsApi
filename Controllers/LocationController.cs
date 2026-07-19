using Microsoft.AspNetCore.Mvc;
using rmrLocationsApi.Models;
using rmrLocationsApi.Services;

namespace rmrLocationsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocationController : ControllerBase
{
    private readonly ILocationService _locationService;
    private readonly ILogger<LocationController> _logger;

    public LocationController(ILocationService locationService, ILogger<LocationController> logger)
    {
        _locationService = locationService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<Location>>> GetAllLocations()
    {
        try
        {
            var locations = await _locationService.GetAllLocationsAsync();
            return Ok(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all locations");
            return StatusCode(500, "An error occurred while retrieving locations.");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Location>> GetLocationById(Guid id)
    {
        try
        {
            var location = await _locationService.GetLocationByIdAsync(id);
            if (location == null)
                return NotFound();
            return Ok(location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting location {LocationId}", id);
            return StatusCode(500, "An error occurred while retrieving the location.");
        }
    }

    [HttpPost]
    public async Task<ActionResult<Location>> CreateLocation([FromBody] Location location)
    {
        try
        {
            if (location == null)
                return BadRequest("Location data is required.");

            var created = await _locationService.AddLocationAsync(location);
            return CreatedAtAction(nameof(GetLocationById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating location");
            return StatusCode(500, "An error occurred while creating the location.");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateLocation(Guid id, [FromBody] Location location)
    {
        try
        {
            if (location == null)
                return BadRequest("Location data is required.");
            if (id != location.Id)
                location.Id = id;

            var updated = await _locationService.UpdateLocationAsync(location);
            if (!updated)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location {LocationId}", id);
            return StatusCode(500, "An error occurred while updating the location.");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLocation(Guid id)
    {
        try
        {
            var deleted = await _locationService.DeleteLocationAsync(id);
            if (!deleted)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting location {LocationId}", id);
            return StatusCode(500, "An error occurred while deleting the location.");
        }
    }

    // Geo-spatial endpoints

    [HttpGet("nearby")]
    public async Task<ActionResult> GetNearby(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusMeters = 5000,
        [FromQuery] int take = 20)
    {
        try
        {
            var results = await _locationService.NearbyAsync(latitude, longitude, radiusMeters, take);
            return Ok(results.Select(r => new { r.Location, DistanceMeters = Math.Round(r.Meters, 1) }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching nearby locations");
            return StatusCode(500, "An error occurred while searching nearby locations.");
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<Location>>> Search(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusMeters = 5000,
        [FromQuery] string categoryIds = "",
        [FromQuery] string tagIds = "")
    {
        try
        {
            var catIds = string.IsNullOrWhiteSpace(categoryIds)
                ? Array.Empty<Guid>()
                : categoryIds.Split(',').Select(Guid.Parse).ToArray();

            var tIds = string.IsNullOrWhiteSpace(tagIds)
                ? Array.Empty<Guid>()
                : tagIds.Split(',').Select(Guid.Parse).ToArray();

            var results = await _locationService.SearchAsync(latitude, longitude, radiusMeters, catIds, tIds);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching locations");
            return StatusCode(500, "An error occurred while searching locations.");
        }
    }

    [HttpPost("{id:guid}/soft-delete")]
    public async Task<IActionResult> SoftDeleteLocation(Guid id)
    {
        try
        {
            var deleted = await _locationService.SoftDeleteLocationAsync(id);
            if (!deleted)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error soft-deleting location {LocationId}", id);
            return StatusCode(500, "An error occurred while soft-deleting the location.");
        }
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> RestoreLocation(Guid id)
    {
        try
        {
            var restored = await _locationService.RestoreLocationAsync(id);
            if (!restored)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring location {LocationId}", id);
            return StatusCode(500, "An error occurred while restoring the location.");
        }
    }
}
