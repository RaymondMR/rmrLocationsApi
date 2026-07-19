namespace rmrLocationsApi.Models;

public class User : AuditableEntity
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }

	public List<string> Roles { get; set; } = [];
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    // 1 : N
    public ICollection<Location> OwnedLocations { get; set; } = new List<Location>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<LocationCollection> Collections { get; set; } = new List<LocationCollection>();

    // N : M explÃ­citos
    public ICollection<UserFavoriteLocation> FavoriteLocations { get; set; } = new List<UserFavoriteLocation>();
    public ICollection<UserCategoryInterest> CategoryInterests { get; set; } = new List<UserCategoryInterest>();
    public ICollection<UserTagFollow> FollowedTags { get; set; } = new List<UserTagFollow>();
}


