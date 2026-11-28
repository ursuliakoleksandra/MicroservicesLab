# Багатоетапна збірка orders-api. Контекст — КОРІНЬ репозиторію:
#   docker build -f Services/Orders/Orders.Api/Dockerfile -t technest/orders-api:latest .
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
# 1) спочатку лише *.csproj — шар restore кешується й не інвалідується зміною вихідного коду
COPY Directory.Build.props ./
COPY Platform.ServiceDefaults/Platform.ServiceDefaults.csproj Platform.ServiceDefaults/
COPY Platform.Messaging/Platform.Messaging.csproj Platform.Messaging/
COPY Platform.Security/Platform.Security.csproj Platform.Security/
COPY Services/Orders/Orders.Domain/Orders.Domain.csproj Services/Orders/Orders.Domain/
COPY Services/Orders/Orders.Dal/Orders.Dal.csproj Services/Orders/Orders.Dal/
COPY Services/Orders/Orders.Bll/Orders.Bll.csproj Services/Orders/Orders.Bll/
COPY Services/Orders/Orders.Api/Orders.Api.csproj Services/Orders/Orders.Api/
RUN dotnet restore Services/Orders/Orders.Api/Orders.Api.csproj
# 2) далі вихідний код і публікація
COPY Platform.ServiceDefaults/ Platform.ServiceDefaults/
COPY Platform.Messaging/ Platform.Messaging/
COPY Platform.Security/ Platform.Security/
COPY Services/Orders/Orders.Domain/ Services/Orders/Orders.Domain/
COPY Services/Orders/Orders.Dal/ Services/Orders/Orders.Dal/
COPY Services/Orders/Orders.Bll/ Services/Orders/Orders.Bll/
COPY Services/Orders/Orders.Api/ Services/Orders/Orders.Api/
RUN dotnet publish Services/Orders/Orders.Api/Orders.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# 3) середовище виконання: лише ASP.NET runtime, без SDK; не root
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Orders.Api.dll"]
