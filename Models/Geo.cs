using NetTopologySuite.Geometries;

namespace rmrLocationsApi.Models;

public static class Geo
{
    public const int Srid = 4326; // WGS 84

    private static readonly GeometryFactory Factory =
        NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(Srid);

    /// <summary>
    /// OJO con el orden: en NTS/PostGIS X = longitud, Y = latitud.
    /// </summary>
    public static Point Point(double latitude, double longitude)
        => Factory.CreatePoint(new Coordinate(longitude, latitude));
}

