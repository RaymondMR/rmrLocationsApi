namespace rmrLocationsApi.Models;

public class LocationMedia : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid LocationId { get; set; }
    public Location? Location { get; set; }

    public string Url { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Caption { get; set; }
    public MediaType Type { get; set; } = MediaType.Image;
    public bool IsCover { get; set; }
    public int SortOrder { get; set; }

    public Guid? UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }
}

