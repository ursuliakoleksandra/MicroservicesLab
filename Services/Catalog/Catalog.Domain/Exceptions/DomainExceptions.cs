namespace Catalog.Domain.Exceptions;

/// <summary>Порушення інваріанта сутності (ціна &lt;= 0, недостатній залишок) -> HTTP 409.</summary>
public class DomainException(string message) : Exception(message);

/// <summary>Ресурс не знайдено -> HTTP 404.</summary>
public class EntityNotFoundException(string message) : Exception(message);

/// <summary>Конфлікт зі станом БД (дублікат SKU, конкурентна зміна) -> HTTP 409.</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>Некоректні вхідні дані, виявлені в BLL -> HTTP 400.</summary>
public class ValidationException(string message) : Exception(message);
