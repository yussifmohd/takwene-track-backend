using Ardalis.Result;
using MediatR;

namespace TrackManagement.Application.Common.Messaging;

/// <summary>A read-only request. Never changes state and never runs inside the Unit of Work.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>
    where TResponse : IResult;
