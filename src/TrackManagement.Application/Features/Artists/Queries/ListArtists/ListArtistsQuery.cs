using Ardalis.Result;
using FluentValidation;
using TrackManagement.Application.Common.Messaging;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Common.Validation;
using TrackManagement.Application.Features.Artists.Specifications;
using PagedResult = TrackManagement.Application.Common.Models.PagedResult<TrackManagement.Application.Features.Artists.ArtistDto>;
using TrackManagement.Domain.Artists;

namespace TrackManagement.Application.Features.Artists.Queries.ListArtists;

/// <param name="IncludeEmail">True only for a signed-in admin (decided at the HTTP edge).</param>
/// <param name="Search">Part of the name, ignoring letter case; null/blank lists everyone.</param>
public sealed record ListArtistsQuery(int Page, int PageSize, bool IncludeEmail, string? Search = null) : IQuery<Result<PagedResult>>;

public sealed class ListArtistsQueryValidator : AbstractValidator<ListArtistsQuery>
{
    public ListArtistsQueryValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        RuleFor(x => x.Search)
            .Must(s => TextRules.CodePointLength(s?.Trim()) <= SearchMaxLength)
            .WithMessage($"Search must be at most {SearchMaxLength} characters.");
    }

    public const int SearchMaxLength = 100;
}

public sealed class ListArtistsQueryHandler(IReadRepository<Artist> artists)
    : IQueryHandler<ListArtistsQuery, Result<PagedResult>>
{
    public async Task<Result<PagedResult>> Handle(ListArtistsQuery request, CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var total = await artists.CountAsync(new ArtistsCountSpec(search), cancellationToken);
        var items = await artists.ListAsync(new ArtistsPageSpec(request.Page, request.PageSize, request.IncludeEmail, search), cancellationToken);
        return Result.Success(new PagedResult(items, request.Page, request.PageSize, total));
    }
}
