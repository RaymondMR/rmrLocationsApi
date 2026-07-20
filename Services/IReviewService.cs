using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

public interface IReviewService
{
    Task<List<Review>> GetReviewsByLocationAsync(Guid locationId);
    Task<Review?> GetReviewByIdAsync(Guid id);
    Task<Review> AddReviewAsync(Review review);
    Task<bool> UpdateReviewAsync(Review review);
    Task<bool> DeleteReviewAsync(Guid id);

    // Authorization: author lookup
    Task<Guid?> GetAuthorIdAsync(Guid reviewId);
}
