using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrackManagement.Application.Features.Artists.Commands.CreateArtist;
using TrackManagement.Application.Features.Tracks.Commands.CreateTrack;
using TrackManagement.Infrastructure.Persistence.Configurations;

namespace TrackManagement.Api.ExceptionHandlers;

/// <summary>
/// A unique index rejected a write (two requests raced past the validator's check). Answer 409 in the
/// same shape as the validator's conflict, naming the field when the index is known.
/// </summary>
public sealed class UniqueConstraintExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private const string UniqueViolation = "23505";

    private static readonly Dictionary<string, (string Field, string Message)> KnownIndexes = new(StringComparer.Ordinal)
    {
        [ArtistConfiguration.NormalizedEmailIndex] = ("email", CreateArtistCommandValidator.DuplicateEmailMessage),
        [TrackConfiguration.IsrcIndex] = ("isrc", CreateTrackCommandValidator.DuplicateIsrcMessage),
        [TrackDistributionConfiguration.PrimaryKey] = ("dspIds", "This platform was just added by someone else. Reload and try again."),
    };

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateException { InnerException: PostgresException { SqlState: UniqueViolation } postgres })
        {
            return false;
        }

        var problem = postgres.ConstraintName is { } name && KnownIndexes.TryGetValue(name, out var known)
            ? new HttpValidationProblemDetails(new Dictionary<string, string[]> { [known.Field] = [known.Message] })
            : new HttpValidationProblemDetails { Detail = "The data conflicts with an existing record." };
        problem.Status = StatusCodes.Status409Conflict;
        problem.Title = "Conflict";

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem });
    }
}
