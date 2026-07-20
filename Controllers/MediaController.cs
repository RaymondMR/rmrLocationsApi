using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using rmrLocationsApi.Extensions;
using rmrLocationsApi.Handlers;
using rmrLocationsApi.Models;
using rmrLocationsApi.Services;

namespace rmrLocationsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // default: escrituras requieren sesión; las lecturas públicas llevan [AllowAnonymous]
public class MediaController : ControllerBase
{
    private readonly IMediaService _mediaService;
    private readonly ILocationService _locationService;
    private readonly ILogger<MediaController> _logger;

    public MediaController(IMediaService mediaService, ILocationService locationService, ILogger<MediaController> logger)
    {
        _mediaService = mediaService;
        _locationService = locationService;
        _logger = logger;
    }

    [HttpGet("location/{locationId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<List<LocationMedia>>> GetMediaByLocation(Guid locationId)
    {
        try
        {
            var media = await _mediaService.GetMediaByLocationAsync(locationId);
            return Ok(media);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting media for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred while retrieving media.");
        }
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<LocationMedia>> GetMediaById(Guid id)
    {
        try
        {
            var media = await _mediaService.GetMediaByIdAsync(id);
            if (media == null)
                return NotFound();
            return Ok(media);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting media {MediaId}", id);
            return StatusCode(500, "An error occurred while retrieving the media.");
        }
    }

    [HttpPost]
    public async Task<ActionResult<LocationMedia>> CreateMedia([FromBody] LocationMedia media)
    {
        try
        {
            if (media == null)
                return BadRequest("Media data is required.");

            var userId = User.GetUserId();
            if (userId is null)
                return Unauthorized();

            // Solo el dueño de la location (o un Admin) puede agregarle media.
            var ownerId = await _locationService.GetOwnerIdAsync(media.LocationId);
            if (ownerId is null)
                return NotFound("Location not found.");
            if (!User.IsAdmin() && userId != ownerId)
                return Forbid();

            if (!User.IsAdmin() || media.UploadedByUserId is null)
                media.UploadedByUserId = userId;

            var created = await _mediaService.AddMediaAsync(media);
            return CreatedAtAction(nameof(GetMediaById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            var mapped = DbErrorMapper.ToActionResult(ex);
            if (mapped is not null)
                return mapped;

            _logger.LogError(ex, "Error creating media");
            return StatusCode(500, "An error occurred while creating the media.");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateMedia(Guid id, [FromBody] LocationMedia media)
    {
        try
        {
            if (media == null)
                return BadRequest("Media data is required.");
            if (id != media.Id)
                media.Id = id;

            var ownerId = await _mediaService.GetLocationOwnerIdAsync(id);
            if (ownerId is null)
                return NotFound();
            if (!User.IsAdmin() && User.GetUserId() != ownerId)
                return Forbid();

            var updated = await _mediaService.UpdateMediaAsync(media);
            if (!updated)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            var mapped = DbErrorMapper.ToActionResult(ex);
            if (mapped is not null)
                return mapped;

            _logger.LogError(ex, "Error updating media {MediaId}", id);
            return StatusCode(500, "An error occurred while updating the media.");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMedia(Guid id)
    {
        try
        {
            var ownerId = await _mediaService.GetLocationOwnerIdAsync(id);
            if (ownerId is null)
                return NotFound();
            if (!User.IsAdmin() && User.GetUserId() != ownerId)
                return Forbid();

            var deleted = await _mediaService.DeleteMediaAsync(id);
            if (!deleted)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting media {MediaId}", id);
            return StatusCode(500, "An error occurred while deleting the media.");
        }
    }
}
