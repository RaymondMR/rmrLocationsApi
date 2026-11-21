using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;

namespace rmrLocationsApi.Validation;

/// <summary>
/// Valida un <see cref="Point"/> geográfico (WGS 84): no vacío, finito y dentro de rango
/// (longitud -180..180, latitud -90..90; en NTS X = longitud, Y = latitud).
/// PostGIS no protege de esto: una coordenada como [-999, 95] o NaN llega a la columna
/// <c>geography(Point,4326)</c> y al índice espacial. <c>null</c> se considera válido aquí
/// porque la obligatoriedad la declara [Required].
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class ValidCoordinatesAttribute : ValidationAttribute
{
    public const double MaxLongitude = 180;
    public const double MaxLatitude = 90;

    public override bool IsValid(object? value)
    {
        if (value is null)
            return true;

        // IsEmpty primero: leer X/Y de un punto vacío lanza InvalidOperationException.
        if (value is not Point { IsEmpty: false } point)
            return false;

        return double.IsFinite(point.X) && double.IsFinite(point.Y)
            && Math.Abs(point.X) <= MaxLongitude
            && Math.Abs(point.Y) <= MaxLatitude;
    }

    public override string FormatErrorMessage(string name)
        => $"The {name} field must be a point with longitude between -{MaxLongitude} and {MaxLongitude} and latitude between -{MaxLatitude} and {MaxLatitude}.";
}
