namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Marcador para Commands CQRS propios (sin MediatR).
/// Un Command produce un efecto de negocio y retorna un resultado.
/// </summary>
public interface ICommand<TResponse>
{
}

/// <summary>
/// Marcador para Commands que no retornan valor (side-effect puro).
/// </summary>
public interface ICommand : ICommand<Unit>
{
}

/// <summary>
/// Handler de un Command que retorna TResponse.
/// </summary>
public interface ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handler de un Command sin retorno.
/// </summary>
public interface ICommandHandler<TCommand> : ICommandHandler<TCommand, Unit>
    where TCommand : ICommand
{
}

/// <summary>
/// Marcador para Queries CQRS propios (sin MediatR).
/// Una Query no produce efectos de negocio — solo lee y retorna datos.
/// </summary>
public interface IQuery<TResponse>
{
}

/// <summary>
/// Handler de una Query que retorna TResponse.
/// </summary>
public interface IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
