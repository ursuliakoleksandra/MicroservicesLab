#!/usr/bin/env bash
# Створює Secret-и у кластері командою `kubectl create secret generic`. Паролі НЕ зберігаються в репозиторії.
# Запуск: SQL_PASSWORD='<пароль SQL Server>' ./deploy/k8s/scripts/create-secrets.sh
#   (необов'язково: MONGO_PASSWORD, RABBITMQ_PASSWORD, KEYCLOAK_ADMIN_PASSWORD, JWT_SECRET, AGGREGATOR_CLIENT_SECRET — інакше генеруються)
set -euo pipefail
NS=technest
: "${SQL_PASSWORD:?Задайте SQL_PASSWORD (мін. 8 символів, великі+малі літери, цифри, спецсимвол — вимога SQL Server)}"
rnd() { openssl rand -base64 36 | tr -d '/+=' | cut -c1-32; }
MONGO_PASSWORD=${MONGO_PASSWORD:-$(rnd)}
RABBITMQ_PASSWORD=${RABBITMQ_PASSWORD:-$(rnd)}
KEYCLOAK_ADMIN_PASSWORD=${KEYCLOAK_ADMIN_PASSWORD:-$(rnd)}
JWT_SECRET=${JWT_SECRET:-$(rnd)$(rnd)}                      # HS256: ≥ 32 символи
AGGREGATOR_CLIENT_SECRET=${AGGREGATOR_CLIENT_SECRET:-$(rnd)}

SQL="Server=sql,1433;User Id=sa;Password=${SQL_PASSWORD};TrustServerCertificate=True;Encrypt=False;Database="
ISQL="Server=identityserver-sql,1433;User Id=sa;Password=${SQL_PASSWORD};TrustServerCertificate=True;Encrypt=False;Database="
MESSAGING="amqp://technest:${RABBITMQ_PASSWORD}@rabbitmq:5672"
MONGO="mongodb://admin:${MONGO_PASSWORD}@mongo:27017/ReviewsDb?authSource=admin"
REDIS="redis:6379"

kubectl apply -f "$(dirname "$0")/../namespace.yaml"
# apply: ідемпотентно (create ... --dry-run=client -o yaml | apply)
secret() { local name=$1; shift; kubectl create secret generic "$name" -n "$NS" "$@" --dry-run=client -o yaml | kubectl apply -f -; }

secret infra-secrets --from-literal=sql-password="$SQL_PASSWORD" --from-literal=mongo-password="$MONGO_PASSWORD" \
  --from-literal=rabbitmq-password="$RABBITMQ_PASSWORD" --from-literal=keycloak-admin-password="$KEYCLOAK_ADMIN_PASSWORD"
secret orders-db-init-secrets --from-literal=ConnectionStrings__OrdersDb="${SQL}master"
secret gateway-secrets        --from-literal=Jwt__SecretKey="$JWT_SECRET"
secret payments-api-secrets   --from-literal=Jwt__SecretKey="$JWT_SECRET"
secret aggregator-secrets     --from-literal=Jwt__SecretKey="$JWT_SECRET" --from-literal=ConnectionStrings__redis="$REDIS" \
  --from-literal=ClientCredentials__ClientSecret="$AGGREGATOR_CLIENT_SECRET"
secret orders-api-secrets     --from-literal=Jwt__SecretKey="$JWT_SECRET" --from-literal=ConnectionStrings__OrdersDb="${SQL}OrdersDb" \
  --from-literal=ConnectionStrings__messaging="$MESSAGING"
secret catalog-api-secrets    --from-literal=Jwt__SecretKey="$JWT_SECRET" --from-literal=ConnectionStrings__CatalogDb="${SQL}CatalogDb" \
  --from-literal=ConnectionStrings__redis="$REDIS" --from-literal=ConnectionStrings__messaging="$MESSAGING"
secret reviews-api-secrets    --from-literal=Jwt__SecretKey="$JWT_SECRET" --from-literal=ConnectionStrings__ReviewsDb="$MONGO" \
  --from-literal=ConnectionStrings__messaging="$MESSAGING"
secret auth-api-secrets       --from-literal=Jwt__SecretKey="$JWT_SECRET" --from-literal=ConnectionStrings__IdentityDb="${SQL}IdentityDb"
secret identityserver-secrets --from-literal=Jwt__SecretKey="$JWT_SECRET" --from-literal=ConnectionStrings__IdentityServerDb="${ISQL}IdentityServerDb" \
  --from-literal=Clients__AggregatorSecret="$AGGREGATOR_CLIENT_SECRET"
echo "Secret-и створено в namespace $NS. Збережіть JWT_SECRET/пароль Keycloak для Pulumi та Postman (у репозиторій їх не додавайте)."
