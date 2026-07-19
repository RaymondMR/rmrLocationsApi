namespace rmrLocationsApi.Models;

public class Address
{
    public string? Name { get; set; }
    public string? Street { get; set; }
    public string? ExteriorNumber { get; set; }
    public string? InteriorNumber { get; set; }
    public string? Neighborhood { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; } // ISO 3166-1 alpha-2
}
