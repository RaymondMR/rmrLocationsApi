using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

public interface ITagService
{
    // Las filas de unión con lugares (locationTags) y los lugares anidados se filtran con LocationVisibility.
    Task<List<Tag>> GetAllTagsAsync(Guid? viewerId = null, bool viewerIsAdmin = false);
    Task<Tag?> GetTagByIdAsync(Guid id, Guid? viewerId = null, bool viewerIsAdmin = false);
    Task<Tag> AddTagAsync(Tag tag);
    Task<bool> UpdateTagAsync(Tag tag);
    Task<bool> DeleteTagAsync(Guid id);
}
