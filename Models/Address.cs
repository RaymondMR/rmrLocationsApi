using System.ComponentModel.DataAnnotations;

namespace rmrLocationsApi.Models;

// Validación de entrada: los máximos replican los de AppDbContext (OwnsOne Address). Sin ellos un
// texto largo llegaba a PostgreSQL y terminaba en un 500 por truncamiento.
public class Address
{
    [StringLength(200)]
    public string? Name { get; set; }

    [StringLength(200)]
    public string? Street { get; set; }

    [StringLength(20)]
    public string? ExteriorNumber { get; set; }

    [StringLength(20)]
    public string? InteriorNumber { get; set; }

    [StringLength(120)]
    public string? Neighborhood { get; set; }

    [StringLength(120)]
    public string? City { get; set; }

    [StringLength(120)]
    public string? State { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    // ISO 3166-1 alpha-2. La columna es char(2): "" se guardaría como dos espacios y "M" como "M ",
    // así que se exige null o exactamente dos letras (\z, no $, para no admitir un "\n" final).
    [StringLength(2, MinimumLength = 2)]
    [RegularExpression(@"^[A-Za-z]{2}\z", ErrorMessage = "The CountryCode field must be a 2-letter ISO 3166-1 alpha-2 code.")]
    public string? CountryCode { get; set; }
}
