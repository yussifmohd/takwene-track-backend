using Ardalis.Result;
using FluentValidation;
using MediatR;
using TrackManagement.Application.Common.Behaviors;

namespace TrackManagement.UnitTests.Application.Common.Behaviors;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_InvalidRequest_ReturnsInvalidResultWithFieldErrorsAndSkipsHandler()
    {
        // Arrange
        var sut = new ValidationBehavior<TestCommand, Result<Guid>>([new TestCommandValidator()]);
        var nextCalled = false;
        RequestHandlerDelegate<Result<Guid>> next = _ => { nextCalled = true; return Task.FromResult(Result.Success(Guid.NewGuid())); };

        // Act
        var result = await sut.Handle(new TestCommand("this name is far too long"), next, TestContext.Current.CancellationToken);

        // Assert
        nextCalled.ShouldBeFalse();
        result.Status.ShouldBe(ResultStatus.Invalid);
        var error = result.ValidationErrors.ShouldHaveSingleItem();
        error.Identifier.ShouldBe("Name");
        error.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsNext()
    {
        // Arrange
        var sut = new ValidationBehavior<TestCommand, Result<Guid>>([new TestCommandValidator()]);
        var id = Guid.NewGuid();

        // Act
        var result = await sut.Handle(new TestCommand("ok"), _ => Task.FromResult(Result.Success(id)), TestContext.Current.CancellationToken);

        // Assert
        result.Value.ShouldBe(id);
    }

    [Fact]
    public async Task Handle_NonGenericResult_ReturnsInvalid()
    {
        // Arrange
        var validator = new InlineValidator<PlainCommand>();
        validator.RuleFor(x => x.Value).GreaterThan(0);
        var sut = new ValidationBehavior<PlainCommand, Result>([validator]);

        // Act
        var result = await sut.Handle(new PlainCommand(0), _ => Task.FromResult(Result.Success()), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("Value");
    }

    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        // Arrange
        var sut = new ValidationBehavior<TestQuery, Result<int>>([]);

        // Act
        var result = await sut.Handle(new TestQuery(1), _ => Task.FromResult(Result.Success(7)), TestContext.Current.CancellationToken);

        // Assert
        result.Value.ShouldBe(7);
    }

    public sealed record PlainCommand(int Value);
}
