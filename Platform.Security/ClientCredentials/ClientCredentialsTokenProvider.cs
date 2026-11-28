using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Platform.Security.ClientCredentials;

public sealed class ClientCredentialsOptions
{
    public const string SectionName = "ClientCredentials";

    /// <summary>URL IdentityServer (емітент), напр. http://localhost:5400.</summary>
    public string Authority { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    /// <summary>Scopes через пробіл.</summary>
    public string Scope { get; set; } = "";
}

/// <summary>
/// OAuth 2.0 Client Credentials Flow: сервіс (Aggregator) отримує токен від IdentityServer за client_id/client_secret.
/// У токені немає sub користувача — лише ідентичність сервісу. Токен кешується до завершення терміну дії.
/// </summary>
public sealed class ClientCredentialsTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<ClientCredentialsOptions> options,
    ILogger<ClientCredentialsTokenProvider> logger)
{
    public const string HttpClientName = "idp-token";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (IsValid()) return _token!;

        await _lock.WaitAsync(ct);
        try
        {
            if (IsValid()) return _token!;

            var o = options.Value;
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{o.Authority.TrimEnd('/')}/connect/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = o.ClientId,
                    ["client_secret"] = o.ClientSecret,
                    ["scope"] = o.Scope
                })
            };

            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Token endpoint returned {(int)response.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            _token = doc.RootElement.GetProperty("access_token").GetString()!;
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 300;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

            logger.LogInformation("Client Credentials token obtained for client {ClientId}, scopes [{Scope}], expires in {ExpiresIn} s",
                o.ClientId, o.Scope, expiresIn);
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    // запас 30 с, щоб токен не протух під час виклику
    private bool IsValid() => _token is not null && DateTimeOffset.UtcNow < _expiresAt.AddSeconds(-30);
}
