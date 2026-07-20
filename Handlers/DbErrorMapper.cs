using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace rmrLocationsApi.Handlers;

/// <summary>
/// Traduce violaciones de constraints de PostgreSQL (únicos, FK, checks) a
/// respuestas HTTP con significado (409/400 + detail) en lugar del 500 genérico.
/// </summary>
public static class DbErrorMapper
{
    public static ObjectResult? ToActionResult(Exception exception)
    {
        var problem = ToProblemDetails(exception);
        return problem is null ? null : new ObjectResult(problem) { StatusCode = problem.Status };
    }

    public static ProblemDetails? ToProblemDetails(Exception exception)
    {
        var pg = FindPostgresException(exception);
        if (pg is null)
            return null;

        return pg.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => Problem(
                StatusCodes.Status409Conflict, "Conflict", UniqueMessage(pg.ConstraintName)),

            // "still referenced": DELETE bloqueado por dependientes (Restrict)
            PostgresErrorCodes.ForeignKeyViolation when pg.Message.Contains("still referenced") => Problem(
                StatusCodes.Status409Conflict, "Conflict", RestrictMessage(pg.ConstraintName)),

            // INSERT/UPDATE que referencia un registro inexistente
            PostgresErrorCodes.ForeignKeyViolation => Problem(
                StatusCodes.Status400BadRequest, "Bad Request",
                "Invalid reference: the related record does not exist."),

            PostgresErrorCodes.CheckViolation => Problem(
                StatusCodes.Status400BadRequest, "Bad Request", CheckMessage(pg.ConstraintName)),

            _ => null
        };
    }

    private static ProblemDetails Problem(int status, string title, string detail)
        => new() { Status = status, Title = title, Detail = detail };

    private static PostgresException? FindPostgresException(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is PostgresException pg)
                return pg;
            exception = exception.InnerException;
        }
        return null;
    }

    // Los nombres de constraints vienen de las migraciones (convención EF Core).
    private static string UniqueMessage(string? constraint) => constraint switch
    {
        "IX_locations_Slug" => "A location with this slug already exists.",
        "IX_categories_Slug" => "A category with this slug already exists.",
        "IX_tags_Name" or "IX_tags_Slug" => "A tag with this name or slug already exists.",
        "IX_reviews_LocationId_UserId" => "This user already has a review for this location.",
        "IX_collections_OwnerId_Name" => "A collection with this name already exists for this user.",
        "IX_location_media_LocationId" => "This location already has a cover media item.",
        "IX_location_categories_LocationId" => "This location already has a primary category.",
        "IX_users_UserName" or "IX_users_Email" => "Username or email already exists.",
        _ => "A record with the same unique value already exists."
    };

    private static string RestrictMessage(string? constraint) => constraint switch
    {
        "FK_location_categories_categories_CategoryId" => "The category has locations assigned and cannot be deleted.",
        "FK_categories_categories_ParentCategoryId" => "The category has subcategories and cannot be deleted.",
        _ => "The record is referenced by other data and cannot be deleted."
    };

    private static string CheckMessage(string? constraint) => constraint switch
    {
        "ck_reviews_rating" => "Rating must be between 1 and 5.",
        "ck_locations_rating" => "AverageRating must be between 0 and 5.",
        _ => "A value violates a database check constraint."
    };
}
