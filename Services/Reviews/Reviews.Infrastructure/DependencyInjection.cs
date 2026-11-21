using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongodb");
        return services;
    }
}
