using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace TrackManagement.Api.ExceptionHandlers;

/// <summary>
/// Someone else saved the same row first (its row version changed since it was loaded): 409 asking the
/// client to reload. Generic wording because any entity with a concurrency token can trigger it.
/// </summary>
public sealed class ConcurrencyExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public const string Message = "This record was changed by someone else. Reload it and try again.";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateConcurrencyException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "Conflict", Detail = Message },
        });
    }
}
