using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

public interface IMediaService
{
    Task<List<LocationMedia>> GetMediaByLocationAsync(Guid locationId);
    Task<LocationMedia?> GetMediaByIdAsync(Guid id);
    Task<LocationMedia> AddMediaAsync(LocationMedia media);
    Task<bool> UpdateMediaAsync(LocationMedia media);
    Task<bool> DeleteMediaAsync(Guid id);
}
