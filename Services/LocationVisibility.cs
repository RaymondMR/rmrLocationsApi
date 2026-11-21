using rmrLocationsApi.Models;

namespace rmrLocationsApi.Services;

/// <summary>
/// Regla ÚNICA de quién puede ver un lugar. Debe usarse en TODA lectura que devuelva datos de un
/// lugar (por id, listados, y también cuando viaja anidado en categorías, tags, colecciones, medios
/// o reseñas); si no, un lugar Draft acaba filtrándose por la ruta que se olvidó.
///
///   - Admin: todos.
///   - Usuario con sesión: los publicados y públicos, más los suyos (cualquier estado).
///   - Anónimo: solo los publicados y públicos.
///
/// Un lugar que no se puede ver se trata como si no existiera (404 / lista vacía): responder 403 o
/// distinguirlo de un id inexistente revelaría que existe. Los parámetros del visitante tienen valor
/// por defecto "anónimo" en los servicios a propósito: olvidarse de pasarlos restringe, no expone.
/// </summary>
public static class LocationVisibility
{
    /// <summary>Filtra en SQL los lugares que el visitante puede ver.</summary>
    public static IQueryable<Location> VisibleTo(this IQueryable<Location> query, Guid? viewerId, bool viewerIsAdmin)
    {
        if (viewerIsAdmin)
            return query;

        return viewerId is Guid uid
            ? query.Where(l => (l.Status == LocationStatus.Published && l.IsPublic) || l.OwnerId == uid)
            : query.Where(l => l.Status == LocationStatus.Published && l.IsPublic);
    }

    /// <summary>La misma regla sobre un lugar ya cargado (datos anidados en otras entidades).</summary>
    public static bool IsVisibleTo(this Location location, Guid? viewerId, bool viewerIsAdmin)
        => viewerIsAdmin
           || (location.Status == LocationStatus.Published && location.IsPublic)
           || (viewerId is Guid uid && location.OwnerId == uid);
}
