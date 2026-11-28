# Багатоетапна збірка aggregator. Контекст — КОРІНЬ репозиторію:
#   docker build -f Platform.Aggregator/Dockerfile -t technest/aggregator:latest .
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
# 1) спочатку лише *.csproj — шар restore кешується й не інвалідується зміною вихідного коду
COPY Directory.Build.props ./
COPY Platform.ServiceDefaults/Platform.ServiceDefaults.csproj Platform.ServiceDefaults/
COPY Platform.Caching/Platform.Caching.csproj Platform.Caching/
COPY Platform.Security/Platform.Security.csproj Platform.Security/
COPY Platform.Aggregator/Platform.Aggregator.csproj Platform.Aggregator/
RUN dotnet restore Platform.Aggregator/Platform.Aggregator.csproj
# 2) далі вихідний код і публікація
COPY Platform.ServiceDefaults/ Platform.ServiceDefaults/
COPY Platform.Caching/ Platform.Caching/
COPY Platform.Security/ Platform.Security/
COPY Platform.Aggregator/ Platform.Aggregator/
# .proto клієнтів gRPC лежать у проєктах сервісів (Protobuf Include="..\Services\...")
COPY Services/Catalog/Catalog.Api/Protos/ Services/Catalog/Catalog.Api/Protos/
COPY Services/Orders/Orders.Api/Protos/ Services/Orders/Orders.Api/Protos/
COPY Services/Reviews/Reviews.WebApi/Protos/ Services/Reviews/Reviews.WebApi/Protos/
RUN dotnet publish Platform.Aggregator/Platform.Aggregator.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# 3) середовище виконання: лише ASP.NET runtime, без SDK; не root
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Platform.Aggregator.dll"]
