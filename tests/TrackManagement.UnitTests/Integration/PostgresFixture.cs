using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using TrackManagement.Infrastructure.Identity;
using TrackManagement.Infrastructure.Persistence;

namespace TrackManagement.UnitTests.Integration;

/// <summary>
/// One real PostgreSQL 17 (same image and initdb settings as docker-compose) for all integration
/// tests, migrated once. Needs Docker. Run only these tests with
/// <c>dotnet test --solution TrackManagement.slnx -- --filter-trait "Category=Integration"</c>,
/// or skip them with <c>--filter-not-trait "Category=Integration"</c>.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string Image = "postgres:17.11-alpine3.24";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image)
        .WithEnvironment("POSTGRES_INITDB_ARGS", "--encoding=UTF8 --locale=C.UTF-8")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    // Same Identity table layout as runtime and design time, or EF sees "pending model changes".
    private static readonly ServiceProvider IdentityServices = new ServiceCollection()
        .Configure<IdentityOptions>(o => o.Stores.SchemaVersion = IdentitySetup.SchemaVersion)
        .BuildServiceProvider();

    public AppDbContext CreateContext(TimeProvider? clock = null, string? connectionString = null) => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseApplicationServiceProvider(IdentityServices)
            .UseNpgsql(NpgsqlConnectionStringDefaults.Apply(connectionString ?? ConnectionString))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new AuditableEntityInterceptor(clock ?? TimeProvider.System))
            .Options);

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync(TestContext.Current.CancellationToken);
        await using var context = CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>A second database on the same server, created with the given encoding.</summary>
    public async Task<string> CreateDatabaseAsync(string name, string encoding)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            $"CREATE DATABASE \"{name}\" WITH TEMPLATE template0 ENCODING '{encoding}' LC_COLLATE 'C' LC_CTYPE 'C'", connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresDatabase : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
