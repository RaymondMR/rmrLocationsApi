namespace rmrLocationsApi.Models;

public class UserCategoryInterest
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    /// <summary>Peso para recomendaciones (0..1).</summary>
    public double Weight { get; set; } = 1d;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

