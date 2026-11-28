# Багатоетапна збірка gateway. Контекст — КОРІНЬ репозиторію:
#   docker build -f Platform.ApiGateway/Dockerfile -t technest/gateway:latest .
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
# 1) спочатку лише *.csproj — шар restore кешується й не інвалідується зміною вихідного коду
COPY Directory.Build.props ./
COPY Platform.ServiceDefaults/Platform.ServiceDefaults.csproj Platform.ServiceDefaults/
COPY Platform.Security/Platform.Security.csproj Platform.Security/
COPY Platform.ApiGateway/Platform.ApiGateway.csproj Platform.ApiGateway/
RUN dotnet restore Platform.ApiGateway/Platform.ApiGateway.csproj
# 2) далі вихідний код і публікація
COPY Platform.ServiceDefaults/ Platform.ServiceDefaults/
COPY Platform.Security/ Platform.Security/
COPY Platform.ApiGateway/ Platform.ApiGateway/
RUN dotnet publish Platform.ApiGateway/Platform.ApiGateway.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# 3) середовище виконання: лише ASP.NET runtime, без SDK; не root
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Platform.ApiGateway.dll"]
