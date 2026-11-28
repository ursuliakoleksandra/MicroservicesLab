# Kubernetes: розгортання TechNest (Лаб. 10)

| Тека | Що це |
|------|-------|
| `deploy/k8s/` | ручні маніфести (`kubectl apply -k deploy/k8s`): namespace, інфраструктура (`infra/`), Job `orders-db-init`, застосунки (`apps/<сервіс>/{deployment,service,configmap}.yaml`) |
| `Platform.AppHost/aspirate-output/` | ті самі ресурси у форматі Aspirate (тека на ресурс + `kustomization.yaml`); **складено вручну** за структурою `aspirate generate` |
| `deploy/k8s/scripts/` | збірка образів, створення Secret-ів (`kubectl create secret`), розгортання |

Правила, що виконуються для всіх застосунків: `replicas: 2`, `imagePullPolicy: IfNotPresent`, `containerPort: 8080`,
liveness `/health/live`, readiness `/health/ready`, startup `/health/startup`, `Service` типу `ClusterIP` (`port: 8080 → targetPort: 8080`, селектор `app: <ім'я>`),
`ConfigMap <ім'я>-env` (ключі `services__<ім'я>__http__0`, …) і `envFrom` (`configMapRef` + `secretRef`). Секретів у маніфестах немає.

## Порядок

```powershell
# 0. Docker Desktop -> Settings -> Kubernetes -> Enable (або kind/minikube)
./deploy/k8s/scripts/build-images.ps1                                   # 9 образів technest/<ім'я>:latest
./deploy/k8s/scripts/create-secrets.ps1 -SqlPassword 'Str0ng!Passw0rd'  # kubectl create secret generic ... (JWT-секрет генерується)
kubectl apply -k deploy/k8s                                      # або: kubectl apply -k Platform.AppHost/aspirate-output
kubectl -n technest get pods -w
kubectl -n technest port-forward svc/gateway 5000:8080           # http://localhost:5000
```

Linux/macOS: `*.sh` з тими самими параметрами через змінні середовища (`SQL_PASSWORD=...`). kind: після збірки `kind load docker-image technest/<ім'я>:latest`.

## Особливості кластера

* **Service discovery** без Aspire: код не змінюється, адреси беруться з `services__<ім'я>__http__0` (`http://orders-api:8080`).
* **gRPC (Aggregator → Orders/Catalog/Reviews)** йде по HTTP/2 без TLS на окремому порту **8081** (`Kestrel__Endpoints__Grpc__Protocols=Http2`);
  REST — на 8080 (`Http1`). Aggregator отримує `Aggregator__GrpcAddressFormat=http://_grpc.{0}` і ключі `services__<ім'я>__grpc__0`.
* **Автентифікація:** `IdentityServer__Authority`/`IssuerUri` = `http://identityserver:8080`, `Keycloak__Authority` = `http://keycloak:8080/realms/TechNestRealm`.
  Для токенів із зовнішнього клієнта додайте в hosts `127.0.0.1 identityserver keycloak` і робіть `port-forward` цих сервісів (щоб `iss` збігався).
  `ASPNETCORE_ENVIRONMENT=Development` потрібен, щоб JwtBearer брав metadata по HTTP (у Production `RequireHttpsMetadata=true`).
* **БД:** `orders-db-init` (Job) створює `OrdersDb`; Catalog, Auth і IdentityServer застосовують міграції (або створюють схему) при старті; `orders-api` стає Ready, коли БД готова.
* **Keycloak** (необов'язково): `kubectl -n technest port-forward svc/keycloak 8080:8080`, потім `pulumi up` (див. `Pulumi/TechNest.Keycloak.Pulumi`).
* RabbitMQ Management UI: `kubectl -n technest port-forward svc/rabbitmq 15672:15672`.
* Очищення: `kubectl delete namespace technest`.
