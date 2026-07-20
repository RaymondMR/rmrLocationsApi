using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace rmrLocationsApi.Handlers;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Constraints de BD que lleguen hasta aquí (p. ej. carrera en register) → 409/400
        var problemDetails = DbErrorMapper.ToProblemDetails(exception);
        if (problemDetails is not null)
        {
            logger.LogWarning(exception, "Database constraint violation mapped to {StatusCode}.", problemDetails.Status);
        }
        else
        {
            logger.LogError(exception, "An unhandled exception occurred.");
            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred. Please try again later."
            };
        }

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
