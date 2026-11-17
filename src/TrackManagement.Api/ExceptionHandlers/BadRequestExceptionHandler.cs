using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TrackManagement.Api.ExceptionHandlers;

/// <summary>
/// Malformed requests (invalid JSON, wrong types, non-numeric query values, body too large) are the
/// client's mistake. Minimal APIs throw <see cref="BadHttpRequestException"/> for them in Development,
/// which used to reach <see cref="GlobalExceptionHandler"/> and come back as 500. Answer with the
/// exception's own status (400/413) as ProblemDetails, without echoing parser details.
/// </summary>
public sealed class BadRequestExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequest)
        {
            return false;
        }

        var status = badRequest.StatusCode;
        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = status == StatusCodes.Status413PayloadTooLarge ? "Request body too large" : "Malformed request",
                Detail = status == StatusCodes.Status413PayloadTooLarge
                    ? "The request body is larger than the server accepts."
                    : "The request body or parameters are malformed.",
            },
        });
    }
}
