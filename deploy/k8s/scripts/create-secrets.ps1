# Створює Secret-и у кластері командою `kubectl create secret generic`. Паролі НЕ зберігаються в репозиторії.
# Запуск: ./deploy/k8s/scripts/create-secrets.ps1 -SqlPassword '<пароль SQL Server>'
param(
  [Parameter(Mandatory)][string]$SqlPassword,     # мін. 8 символів, великі+малі літери, цифри, спецсимвол (вимога SQL Server)
  [string]$MongoPassword, [string]$RabbitMqPassword, [string]$KeycloakAdminPassword, [string]$JwtSecret, [string]$AggregatorClientSecret
)
$ErrorActionPreference = 'Stop'
$ns = 'technest'
function New-Rnd { -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | ForEach-Object { [char]$_ }) }
if (-not $MongoPassword)           { $MongoPassword = New-Rnd }
if (-not $RabbitMqPassword)        { $RabbitMqPassword = New-Rnd }
if (-not $KeycloakAdminPassword)   { $KeycloakAdminPassword = New-Rnd }
if (-not $JwtSecret)               { $JwtSecret = (New-Rnd) + (New-Rnd) }       # HS256: ≥ 32 символи
if (-not $AggregatorClientSecret)  { $AggregatorClientSecret = New-Rnd }

$sql  = "Server=sql,1433;User Id=sa;Password=$SqlPassword;TrustServerCertificate=True;Encrypt=False;Database="
$isql = "Server=identityserver-sql,1433;User Id=sa;Password=$SqlPassword;TrustServerCertificate=True;Encrypt=False;Database="
$messaging = "amqp://technest:${RabbitMqPassword}@rabbitmq:5672"
$mongo = "mongodb://admin:${MongoPassword}@mongo:27017/ReviewsDb?authSource=admin"
$redis = 'redis:6379'

kubectl apply -f (Join-Path $PSScriptRoot '..\namespace.yaml')
function New-Secret([string]$Name, [string[]]$Literals) {
  # create --dry-run | apply: ідемпотентно
  $args_ = @('create','secret','generic',$Name,'-n',$ns) + ($Literals | ForEach-Object { "--from-literal=$_" }) + @('--dry-run=client','-o','yaml')
  kubectl @args_ | kubectl apply -f -
  if ($LASTEXITCODE -ne 0) { throw "secret $Name failed" }
}
New-Secret infra-secrets @("sql-password=$SqlPassword","mongo-password=$MongoPassword","rabbitmq-password=$RabbitMqPassword","keycloak-admin-password=$KeycloakAdminPassword")
New-Secret orders-db-init-secrets @("ConnectionStrings__OrdersDb=${sql}master")
New-Secret gateway-secrets @("Jwt__SecretKey=$JwtSecret")
New-Secret payments-api-secrets @("Jwt__SecretKey=$JwtSecret")
New-Secret aggregator-secrets @("Jwt__SecretKey=$JwtSecret","ConnectionStrings__redis=$redis","ClientCredentials__ClientSecret=$AggregatorClientSecret")
New-Secret orders-api-secrets @("Jwt__SecretKey=$JwtSecret","ConnectionStrings__OrdersDb=${sql}OrdersDb","ConnectionStrings__messaging=$messaging")
New-Secret catalog-api-secrets @("Jwt__SecretKey=$JwtSecret","ConnectionStrings__CatalogDb=${sql}CatalogDb","ConnectionStrings__redis=$redis","ConnectionStrings__messaging=$messaging")
New-Secret reviews-api-secrets @("Jwt__SecretKey=$JwtSecret","ConnectionStrings__ReviewsDb=$mongo","ConnectionStrings__messaging=$messaging")
New-Secret auth-api-secrets @("Jwt__SecretKey=$JwtSecret","ConnectionStrings__IdentityDb=${sql}IdentityDb")
New-Secret identityserver-secrets @("Jwt__SecretKey=$JwtSecret","ConnectionStrings__IdentityServerDb=${isql}IdentityServerDb","Clients__AggregatorSecret=$AggregatorClientSecret")
Write-Host "Secret-и створено в namespace $ns. Збережіть JwtSecret і пароль Keycloak для Pulumi/Postman (у репозиторій їх не додавайте)."
