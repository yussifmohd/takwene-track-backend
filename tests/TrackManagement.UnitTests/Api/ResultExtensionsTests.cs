using System.Text.Json;
using Ardalis.Result;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrackManagement.Api;
using TrackManagement.Api.Extensions;
using TrackManagement.Application.Common.Validation;

namespace TrackManagement.UnitTests.Api;

public sealed class ResultExtensionsTests
{
    [Fact]
    public async Task Ok_ReturnsPlainDtoNotResultWrapper()
    {
        // Act
        var (status, body, _) = await ExecuteAsync(Result.Success(new SampleDto("Amr Diab")).ToHttpResult());

        // Assert
        status.ShouldBe(200);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("name").GetString().ShouldBe("Amr Diab");
        json.RootElement.TryGetProperty("isSuccess", out _).ShouldBeFalse();
        json.RootElement.TryGetProperty("value", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task CreatedHttpResult_Returns201WithLocationAndId()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var (status, body, headers) = await ExecuteAsync(Result.Created(id).ToCreatedHttpResult(x => $"/api/things/{x}"));

        // Assert
        status.ShouldBe(201);
        headers.Location.ToString().ShouldBe($"/api/things/{id}");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("id").GetGuid().ShouldBe(id);
    }

    [Fact]
    public async Task CreatedAtHttpResult_Returns201WithLocationAndTheResourceAsBody()
    {
        var (status, body, headers) = await ExecuteAsync(Result.Created(new SampleDto("Amr Diab")).ToCreatedAtHttpResult(x => $"/api/things/{x.Name}"));

        status.ShouldBe(201);
        headers.Location.ToString().ShouldBe("/api/things/Amr Diab");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("name").GetString().ShouldBe("Amr Diab");
    }

    [Fact]
    public async Task InvalidWithOnlyConflictErrors_Returns409ThatStillNamesTheField()
    {
        var result = Result<Guid>.Invalid(new ValidationError("Email", "Taken.", ValidationErrorCodes.Conflict, ValidationSeverity.Error));

        var (status, body, headers) = await ExecuteAsync(result.ToHttpResult());

        status.ShouldBe(409);
        headers.ContentType!.ToString().ShouldStartWith("application/problem+json");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("title").GetString().ShouldBe("Conflict");
        json.RootElement.GetProperty("errors").GetProperty("email")[0].GetString().ShouldBe("Taken.");
    }

    [Fact]
    public async Task InvalidWithConflictAndOtherErrors_Returns400WithAll()
    {
        var result = Result<Guid>.Invalid(
            new ValidationError("Email", "Taken.", ValidationErrorCodes.Conflict, ValidationSeverity.Error),
            new ValidationError("Name", "Name is required."));

        var (status, body, _) = await ExecuteAsync(result.ToHttpResult());

        status.ShouldBe(400);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("errors").GetProperty("email")[0].GetString().ShouldBe("Taken.");
        json.RootElement.GetProperty("errors").GetProperty("name")[0].GetString().ShouldBe("Name is required.");
    }

    [Fact]
    public async Task NonGenericSuccess_Returns204()
    {
        var (status, _, _) = await ExecuteAsync(Result.Success().ToHttpResult());
        status.ShouldBe(204);
    }

    [Fact]
    public async Task Invalid_ReturnsValidationProblemWithCamelCaseFieldKeys()
    {
        // Arrange
        var result = Result<Guid>.Invalid(
            new ValidationError("Email", "Email is invalid."),
            new ValidationError("Items[0].UnitPrice", "Must be positive."));

        // Act
        var (status, body, headers) = await ExecuteAsync(result.ToHttpResult());

        // Assert
        status.ShouldBe(400);
        headers.ContentType!.ToString().ShouldStartWith("application/problem+json");
        using var json = JsonDocument.Parse(body);
        var errors = json.RootElement.GetProperty("errors");
        errors.GetProperty("email")[0].GetString().ShouldBe("Email is invalid.");
        errors.GetProperty("items[0].unitPrice")[0].GetString().ShouldBe("Must be positive.");
    }

    [Theory]
    [InlineData(ResultStatus.NotFound, 404)]
    [InlineData(ResultStatus.Conflict, 409)]
    [InlineData(ResultStatus.Unauthorized, 401)]
    [InlineData(ResultStatus.Forbidden, 403)]
    [InlineData(ResultStatus.Unavailable, 503)]
    public async Task FailureStatuses_MapToProblemDetails(ResultStatus resultStatus, int expected)
    {
        // Arrange
        Result<int> result = resultStatus switch
        {
            ResultStatus.NotFound => Result<int>.NotFound("Track 42 not found."),
            ResultStatus.Conflict => Result<int>.Conflict("Cannot change status from distributed to draft."),
            ResultStatus.Unauthorized => Result<int>.Unauthorized(),
            ResultStatus.Forbidden => Result<int>.Forbidden(),
            _ => Result<int>.Unavailable("Try later."),
        };

        // Act
        var (status, body, headers) = await ExecuteAsync(result.ToHttpResult());

        // Assert
        status.ShouldBe(expected);
        headers.ContentType!.ToString().ShouldStartWith("application/problem+json");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(expected);
        json.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task NotFound_UsesErrorMessageAsDetail()
    {
        var (_, body, _) = await ExecuteAsync(Result<int>.NotFound("Track 42 not found.").ToHttpResult());
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("detail").GetString().ShouldBe("Track 42 not found.");
    }

    [Fact]
    public async Task Error_Returns500WithoutEchoingInternalMessage()
    {
        // Act
        var (status, body, _) = await ExecuteAsync(Result<int>.Error("Npgsql connection string Password=hunter2").ToHttpResult());

        // Assert
        status.ShouldBe(500);
        body.ShouldNotContain("hunter2");
    }

    [Theory]
    [InlineData("Name", "name")]
    [InlineData("Items[0].UnitPrice", "items[0].unitPrice")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void ToCamelCasePath_ConvertsEachSegment(string? input, string expected) =>
        TrackManagement.Api.Extensions.ResultExtensions.ToCamelCasePath(input).ShouldBe(expected);

    private static async Task<(int Status, string Body, IHeaderDictionary Headers)> ExecuteAsync(Microsoft.AspNetCore.Http.IResult result)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddApi(new ConfigurationBuilder().Build()); // same ProblemDetails + JSON setup as production
        await using var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        await result.ExecuteAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync(), context.Response.Headers);
    }

    private sealed record SampleDto(string Name);
}
