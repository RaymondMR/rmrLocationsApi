using Microsoft.AspNetCore.Mvc;
using rmrLocationsApi.Models;
using rmrLocationsApi.Services;

namespace rmrLocationsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TagController : ControllerBase
{
    private readonly ITagService _tagService;
    private readonly ILogger<TagController> _logger;

    public TagController(ITagService tagService, ILogger<TagController> logger)
    {
        _tagService = tagService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<Tag>>> GetAllTags()
    {
        try
        {
            var tags = await _tagService.GetAllTagsAsync();
            return Ok(tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all tags");
            return StatusCode(500, "An error occurred while retrieving tags.");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Tag>> GetTagById(Guid id)
    {
        try
        {
            var tag = await _tagService.GetTagByIdAsync(id);
            if (tag == null)
                return NotFound();
            return Ok(tag);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tag {TagId}", id);
            return StatusCode(500, "An error occurred while retrieving the tag.");
        }
    }

    [HttpPost]
    public async Task<ActionResult<Tag>> CreateTag([FromBody] Tag tag)
    {
        try
        {
            if (tag == null)
                return BadRequest("Tag data is required.");

            var created = await _tagService.AddTagAsync(tag);
            return CreatedAtAction(nameof(GetTagById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tag");
            return StatusCode(500, "An error occurred while creating the tag.");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTag(Guid id, [FromBody] Tag tag)
    {
        try
        {
            if (tag == null)
                return BadRequest("Tag data is required.");
            if (id != tag.Id)
                tag.Id = id;

            var updated = await _tagService.UpdateTagAsync(tag);
            if (!updated)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tag {TagId}", id);
            return StatusCode(500, "An error occurred while updating the tag.");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTag(Guid id)
    {
        try
        {
            var deleted = await _tagService.DeleteTagAsync(id);
            if (!deleted)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tag {TagId}", id);
            return StatusCode(500, "An error occurred while deleting the tag.");
        }
    }
}
