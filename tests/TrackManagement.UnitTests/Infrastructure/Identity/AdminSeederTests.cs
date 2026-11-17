using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TrackManagement.Infrastructure.Identity;
using TrackManagement.Infrastructure.Persistence;
using TrackManagement.UnitTests.TestSupport;

namespace TrackManagement.UnitTests.Infrastructure.Identity;

public sealed class AdminSeederTests
{
    private const string Password = "Adm1n!Passw0rd#2026";

    [Fact]
    public async Task FirstRun_CreatesAdminRoleAndAccount()
    {
        await using var services = Build("admin@takwene.local", Password);

        await SeedAsync(services);

        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync("admin@takwene.local");
        admin.ShouldNotBeNull();
        (await users.IsInRoleAsync(admin, "Admin")).ShouldBeTrue();
        (await users.CheckPasswordAsync(admin, Password)).ShouldBeTrue();
        admin.LockoutEnabled.ShouldBeTrue();
        users.Users.Count().ShouldBe(1);
    }

    [Fact]
    public async Task SecondRun_WithDifferentPassword_LeavesExistingAccountUnchanged()
    {
        await using var services = Build("admin@takwene.local", Password);
        await SeedAsync(services);

        await SeedAsync(services, new AdminOptions { Email = " ADMIN@takwene.local ", Password = "Other!Passw0rd#2027" });

        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        users.Users.Count().ShouldBe(1);
        (await users.CheckPasswordAsync((await users.FindByEmailAsync("admin@takwene.local"))!, Password)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("", Password, "Admin:Email")]
    [InlineData("admin@takwene.local", "", "Admin:Password")]
    [InlineData("admin@takwene.local", "   ", "Admin:Password")]
    public async Task MissingSetting_FailsNamingTheSetting(string email, string password, string setting)
    {
        await using var services = Build(email, password);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => SeedAsync(services));

        ex.Message.ShouldContain(setting);
    }

    [Theory]
    [InlineData("short1!A")]
    [InlineData("alllowercase1!x")]
    [InlineData("NoDigitsHere!!x")]
    [InlineData("NoSymbols12345x")]
    public async Task WeakPassword_FailsListingRules_WithoutTheValue(string weak)
    {
        await using var services = Build("admin@takwene.local", weak);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => SeedAsync(services));

        ex.Message.ShouldContain("Admin:Password does not meet the password rules");
        ex.Message.ShouldNotContain(weak);
    }

    private static ServiceProvider Build(string email, string password) =>
        TestIdentity.Build(configure: s =>
        {
            s.AddSingleton(Options.Create(new AdminOptions { Email = email, Password = password }));
            s.AddScoped<AdminSeeder>();
        });

    private static async Task SeedAsync(ServiceProvider services, AdminOptions? overrideOptions = null)
    {
        await using var scope = services.CreateAsyncScope();
        var seeder = overrideOptions is null
            ? scope.ServiceProvider.GetRequiredService<AdminSeeder>()
            : ActivatorUtilities.CreateInstance<AdminSeeder>(scope.ServiceProvider, Options.Create(overrideOptions));
        await seeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), TestContext.Current.CancellationToken);
    }
}
