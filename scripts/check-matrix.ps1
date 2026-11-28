<#
 Перевірка матриці прав (Лаб. 09, таблиці 3.3.1–3.3.3): для кожного endpoint і кожної ролі Keycloak (admin, manager, user, guest)
 викликає його через Gateway й порівнює ФАКТИЧНИЙ код із очікуваним за матрицею. Allowed = будь-який код, крім 401/403
 (авторизація виконується раніше за валідацію й пошук, тому 400/404 означає «доступ дозволено»); Forbidden = 403.
 Додатково: Identity-токен Admin (власний Auth Service) і запит без токена (401).
 Запуск (PowerShell 7+), AppHost і `pulumi up` уже виконано:
   ./scripts/check-matrix.ps1 -KeycloakClientSecret '<PostmanClientSecret>' -AdminPassword 'Admin#12345'
 Результат виводиться таблицею й зберігається у matrix-result.csv (вставити в звіт).
#>
param(
    [string]$Gateway = 'http://localhost:5000',
    [string]$Keycloak = 'http://localhost:8080/realms/TechNestRealm',
    [string]$KeycloakClientSecret = '',
    [string]$KeycloakUserPassword = 'Pass#12345',
    [string]$AdminUser = 'admin',
    [string]$AdminPassword = 'Admin#12345'
)

# Endpoint → permission → ролі, яким дозволено (таблиці 8–10 методички)
$endpoints = @(
    @{ Name = 'GET    /api/orders/0           (orders:read)';      Method = 'GET';    Path = '/api/orders/0';           Allowed = 'admin','manager','user' },
    @{ Name = 'POST   /api/orders             (orders:create)';    Method = 'POST';   Path = '/api/orders';             Allowed = 'admin','user';           Body = '{}' },
    @{ Name = 'PUT    /api/orders/0/details   (orders:update)';    Method = 'PUT';    Path = '/api/orders/0/details';   Allowed = 'admin','manager';        Body = '{}' },
    @{ Name = 'DELETE /api/orders/0           (orders:delete)';    Method = 'DELETE'; Path = '/api/orders/0';           Allowed = 'admin' },
    @{ Name = 'POST   /api/orders/0/confirm   (orders:approve)';   Method = 'POST';   Path = '/api/orders/0/confirm';   Allowed = 'admin','manager' },
    @{ Name = 'GET    /api/products           (products:read)';    Method = 'GET';    Path = '/api/products';           Allowed = 'admin','manager','user','guest' },
    @{ Name = 'POST   /api/products           (products:create)';  Method = 'POST';   Path = '/api/products';           Allowed = 'admin';                  Body = '{}' },
    @{ Name = 'PUT    /api/products/0         (products:update)';  Method = 'PUT';    Path = '/api/products/0';         Allowed = 'admin','manager';        Body = '{}' },
    @{ Name = 'DELETE /api/products/0         (products:delete)';  Method = 'DELETE'; Path = '/api/products/0';         Allowed = 'admin' },
    @{ Name = 'POST   /api/products/0/publish (products:publish)'; Method = 'POST';   Path = '/api/products/0/publish'; Allowed = 'admin','manager' },
    @{ Name = 'GET    /api/payments           (payments:read)';    Method = 'GET';    Path = '/api/payments';           Allowed = 'admin' },
    @{ Name = 'POST   /api/payments/process   (payments:process)'; Method = 'POST';   Path = '/api/payments/process';   Allowed = 'admin' },
    @{ Name = 'POST   /api/payments/refund    (payments:refund)';  Method = 'POST';   Path = '/api/payments/refund';    Allowed = 'admin' }
)

function Get-LocalToken([string]$user, [string]$password) {
    $r = Invoke-RestMethod -Method Post -Uri "$Gateway/auth/login" -ContentType 'application/json' `
        -Body (@{ userNameOrEmail = $user; password = $password } | ConvertTo-Json)
    $r.accessToken
}

function Get-KeycloakToken([string]$user) {
    $r = Invoke-RestMethod -Method Post -Uri "$Keycloak/protocol/openid-connect/token" -ContentType 'application/x-www-form-urlencoded' `
        -Body @{ grant_type = 'password'; client_id = 'postman'; client_secret = $KeycloakClientSecret; username = $user; password = $KeycloakUserPassword }
    $r.access_token
}

# суб'єкт → (роль за матрицею, або $null, якщо очікується 401; функція токена)
$subjects = [ordered]@{
    'без токена (401)'      = @{ Role = $null;      Token = $null }
    'Identity: Admin'       = @{ Role = 'admin';    Token = { Get-LocalToken $AdminUser $AdminPassword } }
}
if ($KeycloakClientSecret) {
    $subjects['Keycloak: admin (alice)']   = @{ Role = 'admin';   Token = { Get-KeycloakToken 'alice' } }
    $subjects['Keycloak: manager (bob)']   = @{ Role = 'manager'; Token = { Get-KeycloakToken 'bob' } }
    $subjects['Keycloak: user (carol)']    = @{ Role = 'user';    Token = { Get-KeycloakToken 'carol' } }
    $subjects['Keycloak: guest (dave)']    = @{ Role = 'guest';   Token = { Get-KeycloakToken 'dave' } }
}

# токени отримуються один раз на суб'єкта
$tokens = @{}
foreach ($name in $subjects.Keys) { if ($subjects[$name].Token) { $tokens[$name] = & $subjects[$name].Token } }

$failures = 0
$results = foreach ($endpoint in $endpoints) {
    $row = [ordered]@{ Endpoint = $endpoint.Name }
    foreach ($name in $subjects.Keys) {
        $headers = @{}
        if ($tokens.ContainsKey($name)) { $headers['Authorization'] = 'Bearer ' + $tokens[$name] }
        $params = @{ Method = $endpoint.Method; Uri = "$Gateway$($endpoint.Path)"; Headers = $headers; SkipHttpErrorCheck = $true }
        if ($endpoint.Body) { $params.Body = $endpoint.Body; $params.ContentType = 'application/json' }
        $code = [int](Invoke-WebRequest @params).StatusCode

        $role = $subjects[$name].Role
        $expectedAllowed = $role -and ($endpoint.Allowed -contains $role)
        $isAllowed = $code -notin 401, 403
        $ok = if (-not $role) { $code -eq 401 } elseif ($expectedAllowed) { $isAllowed } else { $code -eq 403 }
        if (-not $ok) { $failures++ }
        $row[$name] = "$code " + $(if ($ok) { '✓' } else { '✗ (очікувано інше)' })
    }
    [pscustomobject]$row
}

$results | Format-Table -AutoSize
$results | ConvertTo-Csv -NoTypeInformation | Set-Content -Encoding utf8 matrix-result.csv
Write-Host "Збережено: matrix-result.csv (вставити в звіт як таблицю)"
if ($failures -gt 0) { Write-Host "Розбіжностей із матрицею: $failures" -ForegroundColor Red; exit 1 }
Write-Host 'Усі відповіді збігаються з матрицею.' -ForegroundColor Green
