using Microsoft.Data.SqlClient;
using Orders.Domain.Exceptions;

namespace Orders.Dal;

/// <summary>
/// Перетворює SqlException на доменні винятки (B.5), щоб raw SQL-повідомлення не потрапляли в API.
/// Номери 5000x — керовані помилки процедур Лаб. 01 (THROW); 2601/2627 — порушення UNIQUE; 547 — FK/CHECK.
/// </summary>
public static class SqlErrorTranslator
{
    public static bool CanTranslate(SqlException ex) =>
        ex.Number is >= 50001 and <= 50010 or 2601 or 2627 or 547;

    public static Exception Translate(SqlException ex) => ex.Number switch
    {
        50001 or 50006 or 50010 => new NotFoundException(ex.Message),                  // Order/Product/Customer not found
        50003 or 50005 or 50007 => new ValidationException(ex.Message),                // unknown status, payment method, items
        50002 or 50004 or 50008 or 50009 => new BusinessConflictException(ex.Message), // transition, delete, RowVersion, edit
        2601 or 2627 => new BusinessConflictException("Record with the same key already exists"),
        547 => new BusinessConflictException("The operation violates a data integrity constraint"),
        _ => ex
    };
}
