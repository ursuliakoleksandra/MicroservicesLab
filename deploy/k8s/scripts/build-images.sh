#!/usr/bin/env bash
# Збирає образи всіх сервісів (контекст — корінь репозиторію). Запуск: ./deploy/k8s/scripts/build-images.sh
set -euo pipefail
cd "$(dirname "$0")/../../.."
declare -A DOCKERFILES=(
  [gateway]=Platform.ApiGateway/Dockerfile
  [aggregator]=Platform.Aggregator/Dockerfile
  [orders-api]=Services/Orders/Orders.Api/Dockerfile
  [catalog-api]=Services/Catalog/Catalog.Api/Dockerfile
  [reviews-api]=Services/Reviews/Reviews.WebApi/Dockerfile
  [auth-api]=Services/Auth/TechNest.Auth.Api/Dockerfile
  [identityserver]=Services/IdentityServer/TechNest.IdentityServer/Dockerfile
  [payments-api]=Services/Payments/TechNest.Payments.Api/Dockerfile
  [orders-db-init]=Platform.DbInit/Dockerfile
)
for name in "${!DOCKERFILES[@]}"; do
  echo ">>> technest/$name:latest"
  docker build -f "${DOCKERFILES[$name]}" -t "technest/$name:latest" .
done
# kind:       kind load docker-image technest/<name>:latest   (для кожного образу)
# Docker Desktop Kubernetes бачить локальні образи одразу (imagePullPolicy: IfNotPresent).
