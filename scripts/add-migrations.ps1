<#
 Генерація EF Core міграцій для Лаб. 09 (запускається один раз з кореня репозиторію; потрібен dotnet-ef: dotnet tool install --global dotnet-ef).
 Міграції створюються без доступу до бази: design-time фабрики (IDesignTimeDbContextFactory) містять фіктивний рядок підключення.
 Після генерації сервіси застосовують їх автоматично під час старту (DatabaseInitializer.ApplyAsync → Database.MigrateAsync).
#>
$ErrorActionPreference = 'Stop'

# 1) Auth Service: IdentityDb (AspNetUsers, AspNetRoles, … + RefreshTokens)
dotnet ef migrations add InitialIdentity `
    --project Services/Auth/TechNest.Auth.Api `
    --context TechNest.Auth.Api.Data.ApplicationDbContext `
    --output-dir Migrations

# 2) IdentityServer: три контексти в одній БД IdentityServerDb
$is = 'Services/IdentityServer/TechNest.IdentityServer'
dotnet ef migrations add InitialIdentityUsers `
    --project $is --context TechNest.IdentityServer.Data.ApplicationDbContext --output-dir Migrations/ApplicationDb
dotnet ef migrations add InitialPersistedGrant `
    --project $is --context Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext --output-dir Migrations/PersistedGrantDb
dotnet ef migrations add InitialConfiguration `
    --project $is --context Duende.IdentityServer.EntityFramework.DbContexts.ConfigurationDbContext --output-dir Migrations/ConfigurationDb

Write-Host 'Міграції створено. Закомітьте теки Migrations/.' -ForegroundColor Green
