using Ardalis.Result;
using MediatR;

namespace TrackManagement.Application.Common.Messaging;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
    where TResponse : IResult;
