using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

public interface IMediaService
{
    // Un lugar que el visitante no puede ver (LocationVisibility) devuelve lista vacía / null.
    Task<List<LocationMedia>> GetMediaByLocationAsync(Guid locationId, Guid? viewerId = null, bool viewerIsAdmin = false);
    Task<LocationMedia?> GetMediaByIdAsync(Guid id, Guid? viewerId = null, bool viewerIsAdmin = false);
    Task<LocationMedia> AddMediaAsync(LocationMedia media);
    Task<bool> UpdateMediaAsync(LocationMedia media);
    Task<bool> DeleteMediaAsync(Guid id);

    // Authorization: owner of the location this media belongs to
    Task<Guid?> GetLocationOwnerIdAsync(Guid mediaId);
}
