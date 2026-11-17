using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TrackManagement.Api;
using TrackManagement.Api.ExceptionHandlers;

namespace TrackManagement.UnitTests.Api;

public sealed class UniqueConstraintExceptionHandlerTests
{
    private static PostgresException UniqueViolation(string constraint) =>
        new("duplicate key value violates unique constraint", "ERROR", "ERROR", "23505", constraintName: constraint);

    private static async Task<(bool Handled, int Status, JsonDocument? Body)> HandleAsync(Exception exception)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddApi(new ConfigurationBuilder().Build());
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = "/api/artists";
        context.Request.Headers.Accept = "application/json";
        context.Response.Body = new MemoryStream();
        context.Features.Set<IHttpResponseFeature>(new HttpResponseFeature());

        var handled = await new UniqueConstraintExceptionHandler(provider.GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>())
            .TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        context.Response.Body.Position = 0;
        var text = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        return (handled, context.Response.StatusCode, text.Length == 0 ? null : JsonDocument.Parse(text));
    }

    [Fact]
    public async Task ArtistEmailIndex_Returns409NamingEmail()
    {
        var (handled, status, body) = await HandleAsync(new DbUpdateException("save failed", UniqueViolation("ix_artists_normalized_email")));

        handled.ShouldBeTrue();
        status.ShouldBe(409);
        body!.RootElement.GetProperty("errors").GetProperty("email")[0].GetString().ShouldBe("An artist with this email already exists.");
    }

    [Fact]
    public async Task UnknownIndex_Returns409WithoutFieldErrors()
    {
        var (handled, status, body) = await HandleAsync(new DbUpdateException("save failed", UniqueViolation("ix_something_else")));

        handled.ShouldBeTrue();
        status.ShouldBe(409);
        if (body!.RootElement.TryGetProperty("errors", out var errors))
        {
            errors.EnumerateObject().ShouldBeEmpty();
        }

        body.RootElement.GetProperty("detail").GetString().ShouldBe("The data conflicts with an existing record.");
    }

    [Fact]
    public async Task OtherDatabaseErrors_AreNotHandled()
    {
        var (handled, _, _) = await HandleAsync(new DbUpdateException("save failed", new InvalidOperationException()));
        handled.ShouldBeFalse();
    }
}
