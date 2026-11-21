using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;
using rmrLocationsApi.Validation;

namespace rmrLocationsApi.Models;

public class Location : AuditableEntity
{
    public Guid Id { get; set; }

    // Validación de entrada (DataAnnotations): MVC la aplica al enlazar el cuerpo de POST/PUT y
    // responde 400 con errors{}. Los máximos replican los de AppDbContext; sin ellos un valor largo
    // llegaba a PostgreSQL y terminaba en un 500 por truncamiento (y en citext ni siquiera se
    // aplica el límite). EF Core también lee [Required]/[StringLength], pero coinciden con el
    // Fluent API, así que el modelo no cambia. [Required] rechaza "" y solo espacios (el
    // requerido implícito de los tipos no anulables sí admite cadenas vacías).
    [Required, StringLength(200)]
    public string Name { get; set; }

    [Required, StringLength(220)]
    public string Slug { get; set; }

    [StringLength(4000)]
    public string? Description { get; set; }

    /// <summary>
    /// Columna <c>geography(Point,4326)</c>: ST_Distance / ST_DWithin devuelven
    /// y aceptan METROS, no grados.
    /// </summary>
    [Required, ValidCoordinates]
    public Point Coordinates { get; set; } = null!;

    public Address Address { get; set; } = new();

    [EnumDataType(typeof(LocationStatus))] // StringEnumConverter admite números: 99 no debe pasar
    public LocationStatus Status { get; set; } = LocationStatus.Draft;
    public bool IsPublic { get; set; } = true;

    // El frontend pinta WebsiteUrl como href: solo se admiten URLs absolutas http(s).
    [HttpUrl, StringLength(2048)]
    public string? WebsiteUrl { get; set; }

    [StringLength(32)]
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

