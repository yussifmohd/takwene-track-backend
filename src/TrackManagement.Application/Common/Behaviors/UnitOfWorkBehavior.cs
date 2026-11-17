using Ardalis.Result;
using MediatR;
using TrackManagement.Application.Common.Messaging;
using TrackManagement.Application.Common.Persistence;

namespace TrackManagement.Application.Common.Behaviors;

/// <summary>
/// Commits the Unit of Work once after a successful command.
/// The <c>TRequest : ICommandMarker</c> constraint means the DI container only closes this
/// behavior over commands; queries never reach it (covered by UnitOfWorkBehaviorTests).
/// Failed commands (Invalid, NotFound, Conflict, ...) are never committed.
/// </summary>
public sealed class UnitOfWorkBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommandMarker
    where TResponse : IResult
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken);

        if (response.Status is ResultStatus.Ok or ResultStatus.Created or ResultStatus.NoContent)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return response;
    }
}
