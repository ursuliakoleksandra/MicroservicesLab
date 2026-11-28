# Одноразова задача ініціалізації OrdersDb (Kubernetes Job). Контекст — КОРІНЬ репозиторію:
#   docker build -f Platform.DbInit/Dockerfile -t technest/orders-db-init:latest .
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
# 1) спочатку *.csproj (кеш restore)
COPY Directory.Build.props ./
COPY Platform.DbInit/Platform.DbInit.csproj Platform.DbInit/
RUN dotnet restore Platform.DbInit/Platform.DbInit.csproj
# 2) код і SQL-скрипти (csproj посилається на ..\db\p1\*.sql)
COPY Platform.DbInit/ Platform.DbInit/
COPY db/p1/ db/p1/
RUN dotnet publish Platform.DbInit/Platform.DbInit.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Platform.DbInit.dll"]
