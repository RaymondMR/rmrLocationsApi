using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using rmrLocationsApi.Data;
using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

public class ReviewService : IReviewService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ReviewService> _logger;
    private readonly ILocationService _locationService;

    public ReviewService(AppDbContext db, ILogger<ReviewService> logger, ILocationService locationService)
    {
        _db = db;
        _logger = logger;
        _locationService = locationService;
    }

    public async Task<List<Review>> GetReviewsByLocationAsync(Guid locationId, Guid? viewerId = null, bool viewerIsAdmin = false)
    {
        // Un lugar que el visitante no puede ver se comporta como un lugar sin reseñas: no se revela que existe.
        if (!await _db.Locations.VisibleTo(viewerId, viewerIsAdmin).AnyAsync(l => l.Id == locationId))
            return [];

        return await _db.Reviews
            .AsNoTracking()
            .Where(r => r.LocationId == locationId)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<Review?> GetReviewByIdAsync(Guid id, Guid? viewerId = null, bool viewerIsAdmin = false)
    {
        var review = await _db.Reviews
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Location)
            .FirstOrDefaultAsync(r => r.Id == id);

        // La reseña de un lugar que el visitante no puede ver es un 404 (y el Location anidado no se filtra).
        return review?.Location is { } location && location.IsVisibleTo(viewerId, viewerIsAdmin) ? review : null;
    }

    public async Task<Guid?> GetAuthorIdAsync(Guid reviewId)
    {
        return await _db.Reviews
            .AsNoTracking()
            .Where(r => r.Id == reviewId)
            .Select(r => (Guid?)r.UserId)
            .FirstOrDefaultAsync();
    }

    public async Task<Review> AddReviewAsync(Review review)
    {
        _db.Reviews.Add(review);
        await _db.SaveChangesAsync();

        // Refresh denormalized rating on location
        await _locationService.RefreshRatingAsync(review.LocationId);

        return review;
    }

    public async Task<bool> UpdateReviewAsync(Review review)
    {
        var existing = await _db.Reviews.FindAsync(review.Id);
        if (existing == null)
            return false;

        existing.Rating = review.Rating;
        existing.Title = review.Title;
        existing.Body = review.Body;
        existing.VisitedOn = review.VisitedOn;

        try
        {
            await _db.SaveChangesAsync();
            await _locationService.RefreshRatingAsync(existing.LocationId);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency error updating review {ReviewId}", review.Id);
            return false;
        }
    }

    public async Task<bool> DeleteReviewAsync(Guid id)
    {
        var existing = await _db.Reviews.FindAsync(id);
        if (existing == null)
            return false;

        var locationId = existing.LocationId;
        _db.Reviews.Remove(existing);
        await _db.SaveChangesAsync();

        // Refresh denormalized rating
        await _locationService.RefreshRatingAsync(locationId);

        return true;
    }
}
