using System.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using rmrLocationsApi.Models;

namespace rmrLocationsApi.Validation;

/// <summary>
/// Contrato JSON que blinda la ENTRADA (cuerpo de POST/PUT) contra el sobre-envío (mass assignment).
///
/// Los controladores enlazan entidades de EF directamente ([FromBody] Location, Review, ...). Sin esto
/// el cliente podía fijar cualquier propiedad: averageRating/reviewCount, el Id, isDeleted y, sobre todo,
/// grafos enteros anidados (owner, reviews, media, category, tag...) que EF insertaba como filas nuevas:
/// usuarios nuevos, reseñas a nombre de otros, categorías o tags sin ser Admin, lugares colados por la
/// ruta de reseñas o medios...
///
/// Al DESERIALIZAR se ignora, en todas las entidades de dominio (rmrLocationsApi.Models):
///   - las navegaciones (propiedades que son otra entidad o una colección de entidades), salvo los hijos
///     que el alta de un lugar sí acepta anidados: locationCategories, locationTags y openingHours;
///   - la contabilidad del servidor: Id, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc y Version;
///   - los contadores derivados: Location.AverageRating, Location.ReviewCount y Tag.UsageCount;
///   - en esos tres hijos, la clave del padre (LocationId) y quién lo añadió (AddedByUserId).
///
/// La SERIALIZACIÓN (las respuestas) no cambia: siguen saliendo todas las propiedades.
/// Es una regla por convención: una entidad o navegación nueva queda protegida sin tocar nada aquí.
/// Lo que el controlador asigna a propósito (OwnerId, Review.UserId, UploadedByUserId) sigue enlazándose.
/// </summary>
public sealed class InputHardeningContractResolver : DefaultContractResolver
{
    public InputHardeningContractResolver()
    {
        // Mismo nombrado que el contrato predeterminado de MVC: las respuestas siguen en camelCase.
        NamingStrategy = new CamelCaseNamingStrategy();
    }

    /// <summary>Contabilidad que mantiene el servidor (AuditableEntity y las tablas de unión).</summary>
    private static readonly HashSet<string> ServerBookkeeping =
        ["Id", "CreatedAtUtc", "UpdatedAtUtc", "IsDeleted", "DeletedAtUtc", "Version"];

    /// <summary>Valores derivados de otras tablas o del propio servidor: el cliente no los fija.</summary>
    private static readonly HashSet<(Type Owner, string Property)> ServerOwned =
    [
        (typeof(Location), nameof(Location.AverageRating)),
        (typeof(Location), nameof(Location.ReviewCount)),
        (typeof(Tag), nameof(Tag.UsageCount)),
        // Hijos del alta de un lugar: el padre lo pone EF al guardar el grafo, no el cliente.
        (typeof(LocationCategory), nameof(LocationCategory.LocationId)),
        (typeof(LocationTag), nameof(LocationTag.LocationId)),
        (typeof(LocationTag), nameof(LocationTag.AddedByUserId)),
        (typeof(OpeningHour), nameof(OpeningHour.LocationId)),
    ];

    /// <summary>Únicas colecciones anidadas que se aceptan en un body (solo ids y banderas).</summary>
    private static readonly HashSet<(Type Owner, string Property)> AcceptedNested =
    [
        (typeof(Location), nameof(Location.LocationCategories)),
        (typeof(Location), nameof(Location.LocationTags)),
        (typeof(Location), nameof(Location.OpeningHours)),
    ];

    /// <summary>
    /// Se decide por el tipo CONCRETO que se deserializa (<paramref name="type"/>), no por el miembro:
    /// Newtonsoft entrega los miembros heredados (IsDeleted, DeletedAtUtc, UpdatedAtUtc, Version...) con
    /// <c>ReflectedType</c> = AuditableEntity, que es abstracta y no cuenta como entidad.
    /// </summary>
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        var properties = base.CreateProperties(type, memberSerialization);

        if (IsEntity(type))
        {
            foreach (var property in properties)
            {
                if (property.UnderlyingName is { } name && property.PropertyType is { } propertyType
                    && IgnoredOnInput(type, name, propertyType))
                    property.ShouldDeserialize = static _ => false; // solo afecta a la entrada: la salida no cambia
            }
        }

        return properties;
    }

    private static bool IgnoredOnInput(Type owner, string name, Type propertyType)
    {
        if (ServerBookkeeping.Contains(name) || ServerOwned.Contains((owner, name)))
            return true;

        return IsNavigation(propertyType) && !AcceptedNested.Contains((owner, name));
    }

    /// <summary>
    /// Entidad de dominio: clase concreta de rmrLocationsApi.Models. Address es un objeto de valor
    /// (se guarda en columnas de la propia fila) y por eso NO cuenta como entidad.
    /// </summary>
    private static bool IsEntity(Type type)
        => type.Namespace == typeof(Location).Namespace && type.IsClass && !type.IsAbstract && type != typeof(Address);

    private static bool IsNavigation(Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (IsEntity(type))
            return true;

        return type != typeof(string)
            && type.IsGenericType
            && typeof(IEnumerable).IsAssignableFrom(type)
            && IsEntity(type.GetGenericArguments()[0]);
    }
}
