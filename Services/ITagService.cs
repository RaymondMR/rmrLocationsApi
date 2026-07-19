using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

public interface ITagService
{
    Task<List<Tag>> GetAllTagsAsync();
    Task<Tag?> GetTagByIdAsync(Guid id);
    Task<Tag> AddTagAsync(Tag tag);
    Task<bool> UpdateTagAsync(Tag tag);
    Task<bool> DeleteTagAsync(Guid id);
}
