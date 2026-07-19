using Microsoft.AspNetCore.Mvc;
using rmrLocationsApi.Models;
using rmrLocationsApi.Services;

namespace rmrLocationsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private readonly IMediaService _mediaService;
    private readonly ILogger<MediaController> _logger;

    public MediaController(IMediaService mediaService, ILogger<MediaController> logger)
    {
        _mediaService = mediaService;
        _logger = logger;
    }

    [HttpGet("location/{locationId:guid}")]
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

            var created = await _mediaService.AddMediaAsync(media);
            return CreatedAtAction(nameof(GetMediaById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
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

            var updated = await _mediaService.UpdateMediaAsync(media);
            if (!updated)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating media {MediaId}", id);
            return StatusCode(500, "An error occurred while updating the media.");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMedia(Guid id)
    {
        try
        {
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
