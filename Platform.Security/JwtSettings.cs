namespace Platform.Security;

/// <summary>
/// Налаштування власних JWT (Частина 1: Auth Service видає, Gateway і мікросервіси перевіряють).
/// Issuer, Audience і SecretKey однакові в усіх учасників; SecretKey приходить із секретного параметра AppHost.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "technest-auth";
    public string Audience { get; set; } = "technest-api";
    public string SecretKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey);
}
