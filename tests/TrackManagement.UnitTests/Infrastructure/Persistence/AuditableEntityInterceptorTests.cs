using Microsoft.EntityFrameworkCore;
using TrackManagement.Domain.Artists;
using TrackManagement.Infrastructure.Persistence;
using TrackManagement.UnitTests.Application.Artists;

namespace TrackManagement.UnitTests.Infrastructure.Persistence;

public sealed class AuditableEntityInterceptorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private static AppDbContext CreateContext(TimeProvider clock) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"audit-{Guid.NewGuid()}")
        .AddInterceptors(new AuditableEntityInterceptor(clock))
        .Options);

    [Fact]
    public async Task Added_WithoutCreationTime_GetsNow_Modified_GetsUpdatedTime()
    {
        // Arrange
        var clock = new FixedTimeProvider(T0);
        await using var context = CreateContext(clock);
        var artist = Artist.Create("Name", "EG", "a@b.eg", DateTime.UtcNow);
        artist.CreatedAtUtc = default; // as if created without a time

        // Act: insert
        context.Add(artist);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        artist.CreatedAtUtc.ShouldBe(T0.UtcDateTime);
        artist.UpdatedAtUtc.ShouldBeNull();

        // Act: modify later
        clock.Now = T0.AddMinutes(5);
        context.Entry(artist).Property(a => a.Name).CurrentValue = "Renamed";
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        artist.CreatedAtUtc.ShouldBe(T0.UtcDateTime);
        artist.UpdatedAtUtc.ShouldBe(T0.AddMinutes(5).UtcDateTime);
    }

    [Fact]
    public async Task Added_WithCreationTime_KeepsIt()
    {
        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await using var context = CreateContext(new FixedTimeProvider(T0));
        var artist = Artist.Create("Name", "EG", "a@b.eg", created);

        context.Add(artist);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        artist.CreatedAtUtc.ShouldBe(created);
    }
}
