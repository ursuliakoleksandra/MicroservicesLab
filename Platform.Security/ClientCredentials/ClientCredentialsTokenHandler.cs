using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Security.ClientCredentials;

/// <summary>Додає Bearer-токен (Client Credentials) до кожного вихідного HTTP/gRPC-виклику.</summary>
public sealed class ClientCredentialsTokenHandler(ClientCredentialsTokenProvider provider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await provider.GetTokenAsync(ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, ct);
    }
}

public static class ClientCredentialsExtensions
{
    public static IServiceCollection AddClientCredentials(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ClientCredentialsOptions>(configuration.GetSection(ClientCredentialsOptions.SectionName));
        services.AddHttpClient(ClientCredentialsTokenProvider.HttpClientName);
        services.AddSingleton<ClientCredentialsTokenProvider>();
        services.AddTransient<ClientCredentialsTokenHandler>();
        return services;
    }
}
