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
public class ReviewController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ILocationService _locationService;
    private readonly ILogger<ReviewController> _logger;

    public ReviewController(IReviewService reviewService, ILocationService locationService, ILogger<ReviewController> logger)
    {
        _reviewService = reviewService;
        _locationService = locationService;
        _logger = logger;
    }

    [HttpGet("location/{locationId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<List<Review>>> GetReviewsByLocation(Guid locationId)
    {
        try
        {
            var reviews = await _reviewService.GetReviewsByLocationAsync(locationId, User.GetUserId(), User.IsAdmin());
            return Ok(reviews);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reviews for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred while retrieving reviews.");
        }
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<Review>> GetReviewById(Guid id)
    {
        try
        {
            var review = await _reviewService.GetReviewByIdAsync(id, User.GetUserId(), User.IsAdmin());
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

            var userId = User.GetUserId();
            if (userId is null)
                return Unauthorized();

            // La reseña pertenece siempre al usuario autenticado (evita suplantación).
            review.UserId = userId.Value;

            // Solo se puede reseñar un lugar que el visitante puede ver: un Draft ajeno se trata como inexistente.
            if (!await _locationService.IsVisibleAsync(review.LocationId, userId, User.IsAdmin()))
                return NotFound("Location not found.");

            var created = await _reviewService.AddReviewAsync(review);
            return CreatedAtAction(nameof(GetReviewById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            var mapped = DbErrorMapper.ToActionResult(ex);
            if (mapped is not null)
                return mapped;

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

            var authorId = await _reviewService.GetAuthorIdAsync(id);
            if (authorId is null)
                return NotFound();
            if (!User.IsAdmin() && User.GetUserId() != authorId)
                return Forbid();

            var updated = await _reviewService.UpdateReviewAsync(review);
            if (!updated)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            var mapped = DbErrorMapper.ToActionResult(ex);
            if (mapped is not null)
                return mapped;

            _logger.LogError(ex, "Error updating review {ReviewId}", id);
            return StatusCode(500, "An error occurred while updating the review.");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteReview(Guid id)
    {
        try
        {
            var authorId = await _reviewService.GetAuthorIdAsync(id);
            if (authorId is null)
                return NotFound();
            if (!User.IsAdmin() && User.GetUserId() != authorId)
                return Forbid();

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
