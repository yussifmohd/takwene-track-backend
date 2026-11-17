using Npgsql;
using TrackManagement.Infrastructure.Persistence;

namespace TrackManagement.UnitTests.Infrastructure.Persistence;

public sealed class NpgsqlConnectionStringDefaultsTests
{
    [Fact]
    public void Apply_WhenGssModeAbsent_DisablesGssEncryption()
    {
        var result = new NpgsqlConnectionStringBuilder(
            NpgsqlConnectionStringDefaults.Apply("Host=db;Database=trackmgmt;Username=u;Password=p"));

        result.GssEncryptionMode.ShouldBe(GssEncryptionMode.Disable);
    }

    [Theory]
    [InlineData("Gss Encryption Mode=Require")]
    [InlineData("GssEncryptionMode=Require")]
    [InlineData("gss encryption mode=require")]
    public void Apply_WhenGssModeExplicit_KeepsIt(string setting)
    {
        var result = new NpgsqlConnectionStringBuilder(NpgsqlConnectionStringDefaults.Apply($"Host=db;{setting}"));

        result.GssEncryptionMode.ShouldBe(GssEncryptionMode.Require);
    }

    [Fact]
    public void Apply_PreservesOtherSettings()
    {
        var result = new NpgsqlConnectionStringBuilder(
            NpgsqlConnectionStringDefaults.Apply("Host=db;Port=5433;Database=trackmgmt;Username=u;Password=p@ss"));

        result.Host.ShouldBe("db");
        result.Port.ShouldBe(5433);
        result.Database.ShouldBe("trackmgmt");
        result.Username.ShouldBe("u");
    }
}
