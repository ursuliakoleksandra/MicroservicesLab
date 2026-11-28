# TechNest.Keycloak.Pulumi

Infrastructure as Code для Keycloak: realm `TechNestRealm`, ресурс-сервери (`orders-api`, `products-api`, `payments-api`) зі scope-ами,
реалм-ролі `admin`/`manager`/`user`/`guest` (composite), політики та дозволи, клієнти (`postman`, `swagger`, `aggregator-service`)
з default scopes, audience mappers і protocol mapper дозволів (`authorization.permissions`), тестові користувачі `alice`/`bob`/`carol`/`dave`.
Матриця «роль → дозволи» — у `Matrix.cs` (payments — лише admin).

```powershell
# 1. Запустити AppHost (піднімає Keycloak на :8080), дочекатися Healthy
# 2. У цій теці:
pulumi login --local
pulumi stack init dev
pulumi config set --secret keycloak:password "<keycloak-admin-password>"   # parameter keycloak-admin-password з AppHost
pulumi config set temporaryPasswords false                                  # true — користувач змінює пароль при першому вході
pulumi preview
pulumi up
pulumi stack output --show-secrets        # RealmUrl, AdminConsoleUrl, ClientIds, ClientSecrets
pulumi destroy                            # очищення після демонстрації
```
