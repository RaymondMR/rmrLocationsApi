using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using rmrLocationsApi.DTOs;
using rmrLocationsApi.Extensions;
using rmrLocationsApi.Handlers;
using rmrLocationsApi.Models;
using rmrLocationsApi.Services;

namespace rmrLocationsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // default: escrituras requieren sesión; las lecturas públicas llevan [AllowAnonymous]
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
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<Location>>> GetAllLocations(
        [FromQuery] string? name = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string tagIds = "",
        [FromQuery] string? status = null,
        [FromQuery] string sort = "name",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            LocationStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<LocationStatus>(status, ignoreCase: true, out var parsed))
                    return BadRequest("Invalid status. Use Draft, Published or Archived.");
                statusFilter = parsed;
            }

            var tIds = string.IsNullOrWhiteSpace(tagIds)
                ? Array.Empty<Guid>()
                : tagIds.Split(',').Select(Guid.Parse).ToArray();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var (items, totalCount) = await _locationService.GetLocationsPageAsync(
                name, statusFilter, categoryId, tIds, sort, page, pageSize,
                User.GetUserId(), User.IsAdmin());

            return Ok(new PagedResult<Location>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all locations");
            return StatusCode(500, "An error occurred while retrieving locations.");
        }
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
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

            var userId = User.GetUserId();
            if (userId is null)
                return Unauthorized();

            // El dueño es quien crea; solo un Admin puede asignar otro dueño explícito.
            if (!User.IsAdmin() || location.OwnerId == Guid.Empty)
                location.OwnerId = userId.Value;

            var created = await _locationService.AddLocationAsync(location);
            return CreatedAtAction(nameof(GetLocationById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            var mapped = DbErrorMapper.ToActionResult(ex);
            if (mapped is not null)
                return mapped;

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

            var ownerId = await _locationService.GetOwnerIdAsync(id);
            if (ownerId is null)
                return NotFound();
            if (!User.IsAdmin() && User.GetUserId() != ownerId)
                return Forbid();

            var updated = await _locationService.UpdateLocationAsync(location);
            if (!updated)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            var mapped = DbErrorMapper.ToActionResult(ex);
            if (mapped is not null)
                return mapped;

            _logger.LogError(ex, "Error updating location {LocationId}", id);
            return StatusCode(500, "An error occurred while updating the location.");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLocation(Guid id)
    {
        try
        {
            var ownerId = await _locationService.GetOwnerIdAsync(id);
            if (ownerId is null)
                return NotFound();
            if (!User.IsAdmin() && User.GetUserId() != ownerId)
                return Forbid();

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
    [AllowAnonymous]
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
    [AllowAnonymous]
    public async Task<ActionResult<List<Location>>> Search(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusMeters = 5000,
        [FromQuery] string categoryIds = "",
        [FromQuery] string tagIds = "",
        [FromQuery] string name = "")
    {
        try
        {
            var catIds = string.IsNullOrWhiteSpace(categoryIds)
                ? Array.Empty<Guid>()
                : categoryIds.Split(',').Select(Guid.Parse).ToArray();

            var tIds = string.IsNullOrWhiteSpace(tagIds)
                ? Array.Empty<Guid>()
                : tagIds.Split(',').Select(Guid.Parse).ToArray();

            var results = await _locationService.SearchAsync(latitude, longitude, radiusMeters, catIds, tIds, name);
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
            var ownerId = await _locationService.GetOwnerIdAsync(id);
            if (ownerId is null)
                return NotFound();
            if (!User.IsAdmin() && User.GetUserId() != ownerId)
                return Forbid();

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
            var ownerId = await _locationService.GetOwnerIdAsync(id);
            if (ownerId is null)
                return NotFound();
            if (!User.IsAdmin() && User.GetUserId() != ownerId)
                return Forbid();

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
