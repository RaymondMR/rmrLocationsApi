using Microsoft.AspNetCore.Mvc;
using rmrLocationsApi.Models;
using rmrLocationsApi.Services;

namespace rmrLocationsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ILogger<ReviewController> _logger;

    public ReviewController(IReviewService reviewService, ILogger<ReviewController> logger)
    {
        _reviewService = reviewService;
        _logger = logger;
    }

    [HttpGet("location/{locationId:guid}")]
    public async Task<ActionResult<List<Review>>> GetReviewsByLocation(Guid locationId)
    {
        try
        {
            var reviews = await _reviewService.GetReviewsByLocationAsync(locationId);
            return Ok(reviews);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reviews for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred while retrieving reviews.");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Review>> GetReviewById(Guid id)
    {
        try
        {
            var review = await _reviewService.GetReviewByIdAsync(id);
            if (review == null)
                return NotFound();
            return Ok(review);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting review {ReviewId}", id);
            return StatusCode(500, "An error occurred while retrieving the review.");
        }
    }

    [HttpPost]
    public async Task<ActionResult<Review>> CreateReview([FromBody] Review review)
    {
        try
        {
            if (review == null)
                return BadRequest("Review data is required.");

            var created = await _reviewService.AddReviewAsync(review);
            return CreatedAtAction(nameof(GetReviewById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating review");
            return StatusCode(500, "An error occurred while creating the review.");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateReview(Guid id, [FromBody] Review review)
    {
        try
        {
            if (review == null)
                return BadRequest("Review data is required.");
            if (id != review.Id)
                review.Id = id;

            var updated = await _reviewService.UpdateReviewAsync(review);
            if (!updated)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating review {ReviewId}", id);
            return StatusCode(500, "An error occurred while updating the review.");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteReview(Guid id)
    {
        try
        {
            var deleted = await _reviewService.DeleteReviewAsync(id);
            if (!deleted)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting review {ReviewId}", id);
            return StatusCode(500, "An error occurred while deleting the review.");
        }
    }
}
