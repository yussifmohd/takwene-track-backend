using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrackManagement.Api;
using TrackManagement.Api.ExceptionHandlers;

namespace TrackManagement.UnitTests.Api;

public sealed class ConcurrencyExceptionHandlerTests
{
    private static async Task<(bool Handled, int Status, JsonDocument? Body)> HandleAsync(Exception exception)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddApi(new ConfigurationBuilder().Build());
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = "/api/tracks/x/status";
        context.Request.Headers.Accept = "application/json";
        context.Response.Body = new MemoryStream();
        context.Features.Set<IHttpResponseFeature>(new HttpResponseFeature());

        var handled = await new ConcurrencyExceptionHandler(provider.GetRequiredService<IProblemDetailsService>())
            .TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        context.Response.Body.Position = 0;
        var text = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        return (handled, context.Response.StatusCode, text.Length == 0 ? null : JsonDocument.Parse(text));
    }

    [Fact]
    public async Task ConcurrencyConflict_Returns409AskingToReload()
    {
        var (handled, status, body) = await HandleAsync(new DbUpdateConcurrencyException("0 rows affected"));

        handled.ShouldBeTrue();
        status.ShouldBe(409);
        body!.RootElement.GetProperty("title").GetString().ShouldBe("Conflict");
        body.RootElement.GetProperty("detail").GetString().ShouldBe("This record was changed by someone else. Reload it and try again.");
        body.RootElement.GetRawText().ShouldNotContain("0 rows affected");
    }

    [Fact]
    public async Task OtherExceptions_AreNotHandled()
    {
        (await HandleAsync(new DbUpdateException("x"))).Handled.ShouldBeFalse();
        (await HandleAsync(new InvalidOperationException())).Handled.ShouldBeFalse();
    }
}
