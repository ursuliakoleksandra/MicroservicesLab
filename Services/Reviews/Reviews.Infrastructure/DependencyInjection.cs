using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Interfaces;
using Reviews.Infrastructure.Persistence;
using Reviews.Infrastructure.Seeding;

namespace Reviews.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        MongoMappings.Register();                                   // BsonClassMap — один раз, до першого звернення до БД

        services.Configure<MongoDbOptions>(config.GetSection("MongoDb"));
        // Aspire передає рядок підключення як ConnectionStrings__ReviewsDb — він має пріоритет над секцією MongoDb
        services.PostConfigure<MongoDbOptions>(o =>
        {
            var fromHost = config.GetConnectionString("ReviewsDb");
            if (!string.IsNullOrWhiteSpace(fromHost)) o.ConnectionString = fromHost;
        });

        services.AddSingleton<MongoDbContext>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IDiscussionRepository, DiscussionRepository>();
        services.AddScoped<IReviewQueries, ReviewQueries>();
        services.AddScoped<IDiscussionQueries, DiscussionQueries>();

        // порядок реєстрації = порядок запуску: спочатку індекси, потім сідінг (обговорення -> відгуки)
        services.AddSingleton<IDataSeeder, DiscussionSeeder>();
        services.AddSingleton<IDataSeeder, ReviewSeeder>();
        services.AddHostedService<IndexCreationService>();
        services.AddHostedService<DataSeedingService>();

        // Лаб. 07: перевірка MongoDB з іменем, тегами й таймаутом. Легка (команда ping, без читання даних).
        // Власна реалізація замість AspNetCore.HealthChecks.MongoDb: вона збігається з версією драйвера MongoDB.Driver 3.x, яку вже використовує сервіс.
        services.AddHealthChecks().AddCheck<MongoHealthCheck>(
            name: "reviews-db",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["database", "mongodb", "ready"],
            timeout: TimeSpan.FromSeconds(5));
        return services;
    }
}
