using Ardalis.Result;
using MediatR;

namespace TrackManagement.Application.Common.Messaging;

/// <summary>A request that changes state. Returns minimal data (an id or a bare Result).</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>, ICommandMarker
    where TResponse : IResult;
