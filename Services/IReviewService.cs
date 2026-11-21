using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

public interface IReviewService
{
    // Las reseñas de un lugar que el visitante no puede ver (LocationVisibility) se tratan como inexistentes.
    Task<List<Review>> GetReviewsByLocationAsync(Guid locationId, Guid? viewerId = null, bool viewerIsAdmin = false);
    Task<Review?> GetReviewByIdAsync(Guid id, Guid? viewerId = null, bool viewerIsAdmin = false);
    Task<Review> AddReviewAsync(Review review);
    Task<bool> UpdateReviewAsync(Review review);
    Task<bool> DeleteReviewAsync(Guid id);

    // Authorization: author lookup
    Task<Guid?> GetAuthorIdAsync(Guid reviewId);
}
