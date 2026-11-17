using Ardalis.Result;
using FluentValidation;
using TrackManagement.Application.Common.Messaging;

namespace TrackManagement.UnitTests.Application.Common.Behaviors;

// Test-only requests used to drive the real MediatR pipeline built by AddApplication().

public sealed record TestCommand(string Name, ResultStatus Outcome = ResultStatus.Ok) : ICommand<Result<Guid>>;

public sealed class TestCommandHandler : ICommandHandler<TestCommand, Result<Guid>>
{
    public Task<Result<Guid>> Handle(TestCommand request, CancellationToken cancellationToken) =>
        Task.FromResult(request.Outcome switch
        {
            ResultStatus.Ok => Result.Success(Guid.NewGuid()),
            ResultStatus.Created => Result.Created(Guid.NewGuid()),
            ResultStatus.NotFound => Result<Guid>.NotFound("missing"),
            ResultStatus.Conflict => Result<Guid>.Conflict("conflict"),
            _ => Result<Guid>.Error("boom"),
        });
}

public sealed class TestCommandValidator : AbstractValidator<TestCommand>
{
    public TestCommandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(10);
}

public sealed record TestQuery(int Value) : IQuery<Result<int>>;

public sealed class TestQueryHandler : IQueryHandler<TestQuery, Result<int>>
{
    public Task<Result<int>> Handle(TestQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(request.Value * 2));
}
