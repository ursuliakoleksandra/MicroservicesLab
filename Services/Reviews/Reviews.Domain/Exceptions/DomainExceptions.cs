namespace Reviews.Domain.Exceptions;

/// <summary>Порушення бізнес-правила домену (рейтинг поза 1..5, порожній текст коментаря) -> HTTP 400.</summary>
public class DomainException(string message) : Exception(message);

/// <summary>Сутність не знайдена -> HTTP 404.</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>Конфлікт: застаріла версія документа (optimistic concurrency) -> HTTP 409.</summary>
public class ConflictException(string message) : Exception(message);
