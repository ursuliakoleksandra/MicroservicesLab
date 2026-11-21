using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

// Рядок підключення передає AppHost (WithReference(ordersDb) -> ConnectionStrings__OrdersDb).
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__OrdersDb")
    ?? throw new InvalidOperationException("ConnectionStrings__OrdersDb is not set (run via Platform.AppHost)");

var scriptsDir = Path.Combine(AppContext.BaseDirectory, "sql");
var scripts = Directory.GetFiles(scriptsDir, "*.sql").OrderBy(Path.GetFileName).ToList();   // 1-schema, 2-procedures, 3-seed
var goSeparator = new Regex(@"^[ \t]*GO[ \t]*(?:--.*)?\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

// SQL Server у контейнері може приймати з'єднання не відразу — кілька спроб
for (var attempt = 1; ; attempt++)
{
    try
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var script in scripts)
        {
            Console.WriteLine($"Applying {Path.GetFileName(script)} ...");
            var batches = goSeparator.Split(await File.ReadAllTextAsync(script))
                .Where(b => !string.IsNullOrWhiteSpace(b))          // GO у скриптах — директива sqlcmd, а не T-SQL: ділимо на пакети
                .ToList();

            foreach (var batch in batches)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = batch;
                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync();
            }
        }

        Console.WriteLine("OrdersDb is ready (schema, procedures, seed applied; scripts are idempotent).");
        return 0;
    }
    catch (SqlException ex) when (attempt < 10)
    {
        Console.WriteLine($"Attempt {attempt} failed: {ex.Message}. Retrying in 3 s ...");
        await Task.Delay(TimeSpan.FromSeconds(3));
    }
}
