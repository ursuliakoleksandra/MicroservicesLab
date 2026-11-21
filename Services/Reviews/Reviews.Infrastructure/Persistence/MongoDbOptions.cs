namespace Reviews.Infrastructure.Persistence;

/// <summary>Секція "MongoDb" в appsettings. Параметри пулу й таймаутів задаються в рядку підключення (maxPoolSize, serverSelectionTimeoutMS).</summary>
public class MongoDbOptions
{
    public string ConnectionString { get; set; } = "";
    public string DatabaseName { get; set; } = "ReviewsDb";
}
