namespace rmrLocationsApi.Models;

public class Category : AuditableEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; }
    public string Slug { get; set; }
    public string? Description { get; set; }
    public string? IconName { get; set; }
    public string? ColorHex { get; set; }
    public int SortOrder { get; set; }

    public Guid? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();

    public ICollection<LocationCategory> LocationCategories { get; set; } = new List<LocationCategory>();
    public ICollection<UserCategoryInterest> InterestedUsers { get; set; } = new List<UserCategoryInterest>();
}

