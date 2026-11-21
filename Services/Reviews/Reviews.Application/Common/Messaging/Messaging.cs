using MediatR;

namespace Reviews.Application.Common.Messaging;

/// <summary>Команда без результату (запис/зміна/видалення).</summary>
public interface ICommand : IRequest { }

/// <summary>Команда з результатом.</summary>
public interface ICommand<TResponse> : IRequest<TResponse> { }

/// <summary>Запит (лише читання, стан не змінює).</summary>
public interface IQuery<TResponse> : IRequest<TResponse> { }
