using Ardalis.Specification;
using TrackManagement.Application.Common.Persistence;
using TrackManagement.Application.Features.Dsps;
using TrackManagement.Application.Features.Dsps.Queries.ListDsps;
using TrackManagement.Application.Features.Dsps.Specifications;
using TrackManagement.Domain.Dsps;

namespace TrackManagement.UnitTests.Application.Dsps;

public sealed class ListDspsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPlatformsSortedByName()
    {
        // Arrange
        var repository = Substitute.For<IReadRepository<Dsp>>();
        repository.ListAsync(Arg.Any<ISpecification<Dsp, DspDto>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((ISpecification<Dsp, DspDto>)ci[0]).Evaluate(Dsp.Seed).ToList());

        // Act
        var result = await new ListDspsQueryHandler(repository).Handle(new ListDspsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Select(d => d.Name).ShouldBe(["Apple Music", "Spotify", "YouTube Music"]);
        await repository.Received(1).ListAsync(Arg.Any<DspsSpec>(), Arg.Any<CancellationToken>());
    }
}
