using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reviews.Application.Common.Behaviors;

namespace Reviews.Application;

public static class DependencyInjection
{
    /// <summary>MediatR зі скануванням обробників, behaviors у фіксованому порядку та валідатори FluentValidation.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.LicenseKey = config["LuckyPenny:LicenseKey"];             // Community-ключ (один для MediatR і AutoMapper)
            cfg.RegisterServicesFromAssembly(assembly);                    // усі IRequestHandler<,>
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));               // 1 — зовнішній
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));            // 2
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));           // 3 — біля обробника
        });

        services.AddValidatorsFromAssembly(assembly);                      // Scoped: валідатори можуть залежати від репозиторіїв
        return services;
    }
}
