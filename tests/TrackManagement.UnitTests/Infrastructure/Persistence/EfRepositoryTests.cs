using Ardalis.Specification;
using Microsoft.EntityFrameworkCore;
using TrackManagement.Infrastructure.Persistence;

namespace TrackManagement.UnitTests.Infrastructure.Persistence;

/// <summary>
/// Guards two things: (1) Ardalis.Specification(.EntityFrameworkCore) 9.3.1 works on EF Core 10
/// (no 10.x line exists – research R3); (2) repositories never save on their own (research R4).
/// </summary>
public sealed class EfRepositoryTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly EfRepository<Widget> _sut;
    private readonly UnitOfWork _unitOfWork;

    public EfRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"repo-{Guid.NewGuid()}")
            .Options;
        _db = new TestDbContext(options);
        _sut = new EfRepository<Widget>(_db);
        _unitOfWork = new UnitOfWork(_db);
    }

    [Fact]
    public async Task AddAsync_WithoutUnitOfWork_DoesNotPersist()
    {
        // Arrange
        var widget = new Widget { Id = Guid.NewGuid(), Name = "a" };

        // Act
        await _sut.AddAsync(widget, TestContext.Current.CancellationToken);

        // Assert
        _db.ChangeTracker.Entries<Widget>().Single().State.ShouldBe(EntityState.Added);
        (await _db.Set<Widget>().AsNoTracking().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task AddAsync_ThenUnitOfWorkSave_Persists()
    {
        // Arrange
        await _sut.AddAsync(new Widget { Id = Guid.NewGuid(), Name = "a" }, TestContext.Current.CancellationToken);

        // Act
        var written = await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        written.ShouldBe(1);
        (await _db.Set<Widget>().AsNoTracking().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task UpdateAndDelete_WithoutUnitOfWork_DoNotPersist()
    {
        // Arrange
        var widget = new Widget { Id = Guid.NewGuid(), Name = "a" };
        _db.Add(widget);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        widget.Name = "b";
        await _sut.UpdateAsync(widget, TestContext.Current.CancellationToken);
        await _sut.DeleteAsync(widget, TestContext.Current.CancellationToken);

        // Assert
        _db.Entry(widget).State.ShouldBe(EntityState.Deleted);
        (await _db.Set<Widget>().AsNoTracking().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task ProjectionSpecification_ListAndCount_WorkOnEfCore10()
    {
        // Arrange
        _db.AddRange(
            new Widget { Id = Guid.NewGuid(), Name = "alpha" },
            new Widget { Id = Guid.NewGuid(), Name = "beta" },
            new Widget { Id = Guid.NewGuid(), Name = "alphabet" });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();

        // Act
        var names = await _sut.ListAsync(new WidgetNamesStartingWith("alpha"), TestContext.Current.CancellationToken);
        var count = await _sut.CountAsync(new WidgetsStartingWith("alpha"), TestContext.Current.CancellationToken);
        var first = await _sut.FirstOrDefaultAsync(new WidgetsStartingWith("beta"), TestContext.Current.CancellationToken);

        // Assert
        names.ShouldBe(["alpha", "alphabet"]);
        count.ShouldBe(2);
        first.ShouldNotBeNull().Name.ShouldBe("beta");
        _db.ChangeTracker.Entries().ShouldBeEmpty(); // AsNoTracking honoured
    }

    public void Dispose() => _db.Dispose();

    public sealed class Widget
    {
        public Guid Id { get; init; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class WidgetsStartingWith : Specification<Widget>
    {
        public WidgetsStartingWith(string prefix) =>
            Query.AsNoTracking().Where(w => w.Name.StartsWith(prefix));
    }

    private sealed class WidgetNamesStartingWith : Specification<Widget, string>
    {
        public WidgetNamesStartingWith(string prefix)
        {
            Query.AsNoTracking().Where(w => w.Name.StartsWith(prefix)).OrderBy(w => w.Name);
            Query.Select(w => w.Name);
        }
    }

    private sealed class TestDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Widget>().HasKey(w => w.Id);
        }
    }
}
