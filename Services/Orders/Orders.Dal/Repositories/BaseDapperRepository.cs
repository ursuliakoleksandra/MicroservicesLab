using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Orders.Dal.Interfaces;

namespace Orders.Dal.Repositories;

/// <summary>
/// Спільний CRUD для Dapper-репозиторіїв — написаний ОДИН раз. Конкретні репозиторії додають лише
/// специфічні запити (multi-mapping, збережені процедури, агрегації).
///
/// Безпека: ім'я таблиці — константа з коду (не вхідні дані) і додатково перевіряється регулярним виразом;
/// усі ЗНАЧЕННЯ передаються лише як параметри.
/// </summary>
public abstract class BaseDapperRepository<T> : RepositoryBase, IGenericRepository<T> where T : class
{
    private static readonly Regex SafeTableName = new(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$", RegexOptions.Compiled);

    private readonly PropertyInfo _idProperty;
    private readonly PropertyInfo[] _insertProperties;
    private readonly string _selectColumns;
    private readonly bool _identityKey;

    /// <param name="tableName">Константа з коду, наприклад "dbo.Orders".</param>
    /// <param name="identityKey">true — Id генерує БД (IDENTITY); false — Id задає застосунок (як у Products).</param>
    /// <param name="dbGeneratedColumns">Стовпці зі значенням за замовчуванням/rowversion — не вставляються.</param>
    protected BaseDapperRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction,
        string tableName, bool identityKey = true, string[]? dbGeneratedColumns = null)
        : base(connection, currentTransaction)
    {
        if (!SafeTableName.IsMatch(tableName))
            throw new ArgumentException("Table name must be a code constant like 'dbo.Orders'.", nameof(tableName));

        TableName = tableName;
        _identityKey = identityKey;
        _idProperty = typeof(T).GetProperty("Id")
                      ?? throw new InvalidOperationException($"{typeof(T).Name} must have an 'Id' property.");

        var generated = new HashSet<string>(dbGeneratedColumns ?? []);
        var columnProperties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && IsColumnType(p.PropertyType)
                        && p.GetCustomAttribute<NotMappedAttribute>() is null)
            .ToArray();

        _selectColumns = string.Join(", ", columnProperties.Select(p => p.Name));
        _insertProperties = columnProperties
            .Where(p => !generated.Contains(p.Name) && !(identityKey && p.Name == "Id"))
            .ToArray();
    }

    protected string TableName { get; }

    /// <summary>Додаткова умова «живих» записів (наприклад, "IsDeleted = 0"). Константа з коду.</summary>
    protected virtual string ActiveFilter => "";

    protected string SelectColumns => _selectColumns;
    private string AndActive => ActiveFilter.Length == 0 ? "" : $" AND {ActiveFilter}";
    private string WhereActive => ActiveFilter.Length == 0 ? "" : $" WHERE {ActiveFilter}";

    protected CommandDefinition Command(string sql, object? parameters = null,
        CommandType? type = null, CancellationToken ct = default) =>
        new(sql, parameters, Transaction, CommandTimeoutSeconds, type, CommandFlags.Buffered, ct);

    public virtual async Task<T?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var sql = $"SELECT {_selectColumns} FROM {TableName} WHERE Id = @Id{AndActive}";
        await EnsureOpenAsync(ct);
        return await Connection.QuerySingleOrDefaultAsync<T>(Command(sql, new { Id = id }, ct: ct));
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        var sql = $"SELECT {_selectColumns} FROM {TableName}{WhereActive} ORDER BY Id";
        await EnsureOpenAsync(ct);
        return (await Connection.QueryAsync<T>(Command(sql, ct: ct))).AsList();
    }

    public virtual async Task<long> AddAsync(T entity, CancellationToken ct = default)
    {
        var columns = string.Join(", ", _insertProperties.Select(p => p.Name));
        var values = string.Join(", ", _insertProperties.Select(p => "@" + p.Name));
        var sql = _identityKey
            ? $"INSERT INTO {TableName} ({columns}) OUTPUT INSERTED.Id VALUES ({values});"
            : $"INSERT INTO {TableName} ({columns}) VALUES ({values});";

        var parameters = new DynamicParameters();
        foreach (var p in _insertProperties) parameters.Add(p.Name, p.GetValue(entity));

        await EnsureOpenAsync(ct);
        var generatedId = await Translated(() => Connection.ExecuteScalarAsync<long?>(Command(sql, parameters, ct: ct)));
        return _identityKey ? generatedId!.Value : Convert.ToInt64(_idProperty.GetValue(entity));
    }

    public virtual async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        var sql = $"DELETE FROM {TableName} WHERE Id = @Id";
        await EnsureOpenAsync(ct);
        return await Translated(() => Connection.ExecuteAsync(Command(sql, new { Id = id }, ct: ct))) > 0;
    }

    private static bool IsColumnType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
               || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid)
               || type == typeof(byte[]);
    }
}
