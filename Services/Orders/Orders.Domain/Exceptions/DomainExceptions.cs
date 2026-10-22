namespace Orders.Domain.Exceptions;

/// <summary>Ресурс не знайдено -> HTTP 404.</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>Порушення бізнес-правила / конфлікт стану (недопустимий перехід статусу, нестача товару,
/// дублікат ключа, конфлікт RowVersion) -> HTTP 409.</summary>
public class BusinessConflictException(string message) : Exception(message);

/// <summary>Некоректні вхідні дані, виявлені в BLL -> HTTP 400.</summary>
public class ValidationException(string message) : Exception(message);
