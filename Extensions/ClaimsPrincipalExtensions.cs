using System.Security.Claims;
using rmrLocationsApi.Models;

namespace rmrLocationsApi.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Id del usuario autenticado (claim "sub" → NameIdentifier), o null si no hay sesión válida.
    /// </summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static bool IsAdmin(this ClaimsPrincipal principal)
        => principal.IsInRole(UserRoles.Admin);
}
