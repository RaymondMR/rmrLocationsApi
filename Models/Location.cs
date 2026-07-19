using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

namespace rmrLocationsApi.Models;

public class Location : AuditableEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; }
    public string Slug { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// Columna <c>geography(Point,4326)</c>: ST_Distance / ST_DWithin devuelven
    /// y aceptan METROS, no grados.
    /// </summary>
    public Point Coordinates { get; set; } = null!;

    public Address Address { get; set; } = new();

    public LocationStatus Status { get; set; } = LocationStatus.Draft;
    public bool IsPublic { get; set; } = true;

    public string? WebsiteUrl { get; set; }
    public string? PhoneNumber { get; set; }

    // Denormalizados para ordenar/filtrar sin agregaciones costosas
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }

    // N : 1 â€” dueÃ±o (lado "muchos": FK + navegaciÃ³n inversa)
    public Guid OwnerId { get; set; }
    public User? Owner { get; set; }

    // N : M
    public ICollection<LocationCategory> LocationCategories { get; set; } = new List<LocationCategory>();
    public ICollection<LocationTag> LocationTags { get; set; } = new List<LocationTag>();
    public ICollection<UserFavoriteLocation> FavoritedBy { get; set; } = new List<UserFavoriteLocation>();
    public ICollection<CollectionItem> CollectionItems { get; set; } = new List<CollectionItem>();

    // 1 : N
    public ICollection<LocationMedia> Media { get; set; } = new List<LocationMedia>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<OpeningHour> OpeningHours { get; set; } = new List<OpeningHour>();

    // Comodidad para DTOs; no se mapean a columnas
    [NotMapped] public double Latitude => Coordinates?.Y ?? 0;
    [NotMapped] public double Longitude => Coordinates?.X ?? 0;

    public void SetCoordinates(double latitude, double longitude)
        => Coordinates = Geo.Point(latitude, longitude);
}

