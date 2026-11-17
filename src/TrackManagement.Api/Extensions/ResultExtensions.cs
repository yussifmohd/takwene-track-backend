using Ardalis.Result;
using Microsoft.AspNetCore.Http.HttpResults;
using TrackManagement.Application.Common.Validation;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;
using IArdalisResult = Ardalis.Result.IResult;

namespace TrackManagement.Api.Extensions;

/// <summary>
/// The single Result → HTTP mapping. Successes return plain DTOs (never the Result wrapper);
/// every failure becomes RFC 7807 ProblemDetails (validation failures as a ValidationProblem).
/// </summary>
public static class ResultExtensions
{
    public static IHttpResult ToHttpResult<T>(this Result<T> result) => result.Status switch
    {
        ResultStatus.Ok => TypedResults.Ok(result.Value),
        ResultStatus.Created => TypedResults.Created(result.Location, result.Value),
        ResultStatus.NoContent => TypedResults.NoContent(),
        _ => ToProblem(result),
    };

    public static IHttpResult ToHttpResult(this Result result) => result.Status switch
    {
        ResultStatus.Ok or ResultStatus.NoContent => TypedResults.NoContent(),
        ResultStatus.Created => TypedResults.Created(result.Location),
        _ => ToProblem(result),
    };

    /// <summary>Maps a successful create to 201 with a Location header and a <c>{ "id": ... }</c> body.</summary>
    public static IHttpResult ToCreatedHttpResult<TId>(this Result<TId> result, Func<TId, string> location) =>
        result.Status is ResultStatus.Ok or ResultStatus.Created
            ? TypedResults.Created(location(result.Value), new CreatedResponse<TId>(result.Value))
            : ToProblem(result);

    /// <summary>Maps a successful create to 201 with a Location header and the created resource as body.</summary>
    public static IHttpResult ToCreatedAtHttpResult<T>(this Result<T> result, Func<T, string> location) =>
        result.Status is ResultStatus.Ok or ResultStatus.Created
            ? TypedResults.Created(location(result.Value), result.Value)
            : ToProblem(result);

    private static IHttpResult ToProblem(IArdalisResult result) => result.Status switch
    {
        // Every failure is a clash with existing data (e.g. duplicate email): 409, still per field.
        ResultStatus.Invalid when IsConflictOnly(result.ValidationErrors) => TypedResults.Problem(
            new HttpValidationProblemDetails(ToValidationErrors(result.ValidationErrors))
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
            }),
        ResultStatus.Invalid => TypedResults.ValidationProblem(
            ToValidationErrors(result.ValidationErrors),
            title: "One or more validation errors occurred."),
        ResultStatus.NotFound => Problem(StatusCodes.Status404NotFound, "Resource not found", result.Errors),
        ResultStatus.Conflict => Problem(StatusCodes.Status409Conflict, "Conflict", result.Errors),
        ResultStatus.Unauthorized => Problem(StatusCodes.Status401Unauthorized, "Unauthorized", result.Errors),
        ResultStatus.Forbidden => Problem(StatusCodes.Status403Forbidden, "Forbidden", result.Errors),
        ResultStatus.Unavailable => Problem(StatusCodes.Status503ServiceUnavailable, "Service unavailable", result.Errors),
        // Error/CriticalError messages are never echoed: they may contain internal details.
        _ => TypedResults.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "An unexpected error occurred",
            detail: "An unexpected error occurred. Please try again later."),
    };

    private static bool IsConflictOnly(IEnumerable<ValidationError> errors)
    {
        var list = errors.ToList();
        return list.Count > 0 && list.All(e => e.ErrorCode == ValidationErrorCodes.Conflict);
    }

    private static ProblemHttpResult Problem(int statusCode, string title, IEnumerable<string> errors)
    {
        var detail = string.Join(" ", errors.Where(e => !string.IsNullOrWhiteSpace(e)));
        return TypedResults.Problem(statusCode: statusCode, title: title, detail: detail.Length == 0 ? null : detail);
    }

    internal static Dictionary<string, string[]> ToValidationErrors(IEnumerable<ValidationError> errors) =>
        errors
            .GroupBy(e => ToCamelCasePath(e.Identifier))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

    /// <summary>"Items[0].UnitPrice" → "items[0].unitPrice" so keys match the JSON the client sent.</summary>
    internal static string ToCamelCasePath(string? identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return string.Empty;
        }

        return string.Join('.', identifier.Split('.').Select(s => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..]));
    }
}

/// <summary>Body returned by create endpoints.</summary>
public sealed record CreatedResponse<TId>(TId Id);
