using Ardalis.Result;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrackManagement.Application;
using TrackManagement.Application.Common.Behaviors;
using TrackManagement.Application.Common.Persistence;

namespace TrackManagement.UnitTests.Application.Common.Behaviors;

/// <summary>
/// Proves the Unit of Work runs for commands only (selected through the ICommandMarker
/// constraint) and only commits successful commands. Uses the production pipeline registration.
/// </summary>
public sealed class UnitOfWorkBehaviorTests : IDisposable
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ServiceProvider _provider;
    private readonly ISender _sender;

    public UnitOfWorkBehaviorTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddApplication();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<TestCommand>());
        services.AddTransient<FluentValidation.IValidator<TestCommand>, TestCommandValidator>();
        services.AddSingleton(_unitOfWork);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _sender = _provider.GetRequiredService<ISender>();
    }

    [Fact]
    public void Pipeline_ForQuery_DoesNotContainUnitOfWorkBehavior()
    {
        // Act
        var behaviors = _provider.GetServices<IPipelineBehavior<TestQuery, Result<int>>>().ToList();

        // Assert
        behaviors.ShouldNotBeEmpty();
        // UnitOfWorkBehavior<TestQuery, ...> cannot even be constructed: the compiler rejects it because
        // TestQuery is not an ICommandMarker. At runtime the container must likewise skip it.
        behaviors.ShouldNotContain(b => b.GetType().GetGenericTypeDefinition() == typeof(UnitOfWorkBehavior<,>));
        behaviors.Select(b => b.GetType().GetGenericTypeDefinition())
            .ShouldBe([typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)]);
    }

    [Fact]
    public void Pipeline_ForCommand_RunsLoggingThenValidationThenUnitOfWork()
    {
        // Act
        var behaviors = _provider.GetServices<IPipelineBehavior<TestCommand, Result<Guid>>>().ToList();

        // Assert
        behaviors.Select(b => b.GetType().GetGenericTypeDefinition())
            .ShouldBe([typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>), typeof(UnitOfWorkBehavior<,>)]);
    }

    [Fact]
    public async Task Send_Query_NeverCommits()
    {
        // Act
        var result = await _sender.Send(new TestQuery(21), TestContext.Current.CancellationToken);

        // Assert
        result.Value.ShouldBe(42);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ResultStatus.Ok)]
    [InlineData(ResultStatus.Created)]
    public async Task Send_SuccessfulCommand_CommitsExactlyOnce(ResultStatus outcome)
    {
        // Act
        var result = await _sender.Send(new TestCommand("ok", outcome), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(outcome);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ResultStatus.NotFound)]
    [InlineData(ResultStatus.Conflict)]
    [InlineData(ResultStatus.Error)]
    public async Task Send_FailedCommand_DoesNotCommit(ResultStatus outcome)
    {
        // Act
        var result = await _sender.Send(new TestCommand("ok", outcome), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(outcome);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_InvalidCommand_ReturnsInvalidAndDoesNotCommit()
    {
        // Act
        var result = await _sender.Send(new TestCommand(string.Empty), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ValidationErrors.ShouldContain(e => e.Identifier == nameof(TestCommand.Name));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    public void Dispose() => _provider.Dispose();
}
