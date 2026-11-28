# Збирає образи всіх сервісів (контекст — корінь репозиторію). Запуск: ./deploy/k8s/scripts/build-images.ps1
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..\..\..')
$images = [ordered]@{
  'gateway'        = 'Platform.ApiGateway/Dockerfile'
  'aggregator'     = 'Platform.Aggregator/Dockerfile'
  'orders-api'     = 'Services/Orders/Orders.Api/Dockerfile'
  'catalog-api'    = 'Services/Catalog/Catalog.Api/Dockerfile'
  'reviews-api'    = 'Services/Reviews/Reviews.WebApi/Dockerfile'
  'auth-api'       = 'Services/Auth/TechNest.Auth.Api/Dockerfile'
  'identityserver' = 'Services/IdentityServer/TechNest.IdentityServer/Dockerfile'
  'payments-api'   = 'Services/Payments/TechNest.Payments.Api/Dockerfile'
  'orders-db-init' = 'Platform.DbInit/Dockerfile'
}
foreach ($name in $images.Keys) {
  Write-Host ">>> technest/${name}:latest"
  docker build -f $images[$name] -t "technest/${name}:latest" .
  if ($LASTEXITCODE -ne 0) { throw "docker build failed for $name" }
}
# kind: kind load docker-image technest/<name>:latest (для кожного образу). Docker Desktop Kubernetes бачить локальні образи одразу.
