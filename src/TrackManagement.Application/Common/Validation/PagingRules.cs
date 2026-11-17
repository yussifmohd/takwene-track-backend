using FluentValidation;
using TrackManagement.Application.Common.Models;

namespace TrackManagement.Application.Common.Validation;

public static class PagingRules
{
    public static IRuleBuilderOptions<T, int> ValidPage<T>(this IRuleBuilder<T, int> rule) =>
        rule.GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or more.");

    public static IRuleBuilderOptions<T, int> ValidPageSize<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(1, PagedResult<object>.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {PagedResult<object>.MaxPageSize}.");
}
