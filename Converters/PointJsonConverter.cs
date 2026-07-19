using NetTopologySuite.Geometries;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace rmrLocationsApi.Converters;

/// <summary>
/// Newtonsoft.Json converter for NetTopologySuite Point.
/// Accepts GeoJSON format: {"type":"Point","coordinates":[lng,lat]}
/// or simple format: {"X": lng, "Y": lat} or {"lng": lng, "lat": lat}
/// </summary>
public class PointJsonConverter : JsonConverter<Point>
{
    public override Point? ReadJson(JsonReader reader, Type objectType, Point? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        var obj = JObject.Load(reader);

        // GeoJSON format: {"type":"Point","coordinates":[lng,lat]}
        if (obj["type"]?.ToString() == "Point" && obj["coordinates"] is JArray coords && coords.Count >= 2)
        {
            var lng = coords[0].Value<double>();
            var lat = coords[1].Value<double>();
            return new Point(lng, lat) { SRID = 4326 };
        }

        // Simple format: {"X": lng, "Y": lat}
        if (obj["X"] != null && obj["Y"] != null)
        {
            var lng = obj["X"]!.Value<double>();
            var lat = obj["Y"]!.Value<double>();
            return new Point(lng, lat) { SRID = 4326 };
        }

        // Alternative: {"lng": lng, "lat": lat} or {"longitude": lng, "latitude": lat}
        double longitude = 0, latitude = 0;
        bool hasLng = false, hasLat = false;

        if (obj["lng"] != null) { longitude = obj["lng"]!.Value<double>(); hasLng = true; }
        else if (obj["longitude"] != null) { longitude = obj["longitude"]!.Value<double>(); hasLng = true; }
        else if (obj["Longitude"] != null) { longitude = obj["Longitude"]!.Value<double>(); hasLng = true; }

        if (obj["lat"] != null) { latitude = obj["lat"]!.Value<double>(); hasLat = true; }
        else if (obj["latitude"] != null) { latitude = obj["latitude"]!.Value<double>(); hasLat = true; }
        else if (obj["Latitude"] != null) { latitude = obj["Latitude"]!.Value<double>(); hasLat = true; }

        if (hasLng && hasLat)
            return new Point(longitude, latitude) { SRID = 4326 };

        throw new JsonSerializationException($"Cannot deserialize Point from: {obj}");
    }

    public override void WriteJson(JsonWriter writer, Point? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName("type");
        writer.WriteValue("Point");
        writer.WritePropertyName("coordinates");
        writer.WriteStartArray();
        writer.WriteValue(value.X); // longitude
        writer.WriteValue(value.Y); // latitude
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
