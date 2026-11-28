# Багатоетапна збірка catalog-api. Контекст — КОРІНЬ репозиторію:
#   docker build -f Services/Catalog/Catalog.Api/Dockerfile -t technest/catalog-api:latest .
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
# 1) спочатку лише *.csproj — шар restore кешується й не інвалідується зміною вихідного коду
COPY Directory.Build.props ./
COPY Platform.ServiceDefaults/Platform.ServiceDefaults.csproj Platform.ServiceDefaults/
COPY Platform.Caching/Platform.Caching.csproj Platform.Caching/
COPY Platform.Messaging/Platform.Messaging.csproj Platform.Messaging/
COPY Platform.Security/Platform.Security.csproj Platform.Security/
COPY Services/Catalog/Catalog.Domain/Catalog.Domain.csproj Services/Catalog/Catalog.Domain/
COPY Services/Catalog/Catalog.Dal/Catalog.Dal.csproj Services/Catalog/Catalog.Dal/
COPY Services/Catalog/Catalog.Bll/Catalog.Bll.csproj Services/Catalog/Catalog.Bll/
COPY Services/Catalog/Catalog.Api/Catalog.Api.csproj Services/Catalog/Catalog.Api/
RUN dotnet restore Services/Catalog/Catalog.Api/Catalog.Api.csproj
# 2) далі вихідний код і публікація
COPY Platform.ServiceDefaults/ Platform.ServiceDefaults/
COPY Platform.Caching/ Platform.Caching/
COPY Platform.Messaging/ Platform.Messaging/
COPY Platform.Security/ Platform.Security/
COPY Services/Catalog/Catalog.Domain/ Services/Catalog/Catalog.Domain/
COPY Services/Catalog/Catalog.Dal/ Services/Catalog/Catalog.Dal/
COPY Services/Catalog/Catalog.Bll/ Services/Catalog/Catalog.Bll/
COPY Services/Catalog/Catalog.Api/ Services/Catalog/Catalog.Api/
RUN dotnet publish Services/Catalog/Catalog.Api/Catalog.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# 3) середовище виконання: лише ASP.NET runtime, без SDK; не root
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Catalog.Api.dll"]
