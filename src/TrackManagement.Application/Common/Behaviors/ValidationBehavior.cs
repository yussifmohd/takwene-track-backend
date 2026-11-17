using System.Reflection;
using Ardalis.Result;
using FluentValidation;
using MediatR;

namespace TrackManagement.Application.Common.Behaviors;

/// <summary>
/// Runs every FluentValidation validator for the request (commands and queries). Failures become
/// <see cref="Result.Invalid(ValidationError[])"/> instead of exceptions, so the HTTP edge maps them
/// to a 400 ValidationProblem and the handler (and Unit of Work) never run.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResult
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count == 0)
        {
            return await next(cancellationToken);
        }

        var errors = failures
            .Select(f => new ValidationError(f.PropertyName, f.ErrorMessage, f.ErrorCode, ValidationSeverity.Error))
            .ToList();

        return CreateInvalid(errors);
    }

    private static TResponse CreateInvalid(List<ValidationError> errors)
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)(IResult)Result.Invalid(errors);
        }

        // Result<T>: call the static Result<T>.Invalid(IEnumerable<ValidationError>) on the closed type.
        var invalid = typeof(TResponse)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(Result.Invalid)
                         && m.GetParameters() is [{ ParameterType: var p }]
                         && p.IsAssignableFrom(typeof(List<ValidationError>))
                         && !p.IsArray);

        return (TResponse)invalid.Invoke(null, [errors])!;
    }
}
