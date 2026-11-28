# Багатоетапна збірка reviews-api. Контекст — КОРІНЬ репозиторію:
#   docker build -f Services/Reviews/Reviews.WebApi/Dockerfile -t technest/reviews-api:latest .
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
# 1) спочатку лише *.csproj — шар restore кешується й не інвалідується зміною вихідного коду
COPY Directory.Build.props ./
COPY Platform.ServiceDefaults/Platform.ServiceDefaults.csproj Platform.ServiceDefaults/
COPY Platform.Messaging/Platform.Messaging.csproj Platform.Messaging/
COPY Platform.Security/Platform.Security.csproj Platform.Security/
COPY Services/Reviews/Reviews.Domain/Reviews.Domain.csproj Services/Reviews/Reviews.Domain/
COPY Services/Reviews/Reviews.Application/Reviews.Application.csproj Services/Reviews/Reviews.Application/
COPY Services/Reviews/Reviews.Infrastructure/Reviews.Infrastructure.csproj Services/Reviews/Reviews.Infrastructure/
COPY Services/Reviews/Reviews.WebApi/Reviews.WebApi.csproj Services/Reviews/Reviews.WebApi/
RUN dotnet restore Services/Reviews/Reviews.WebApi/Reviews.WebApi.csproj
# 2) далі вихідний код і публікація
COPY Platform.ServiceDefaults/ Platform.ServiceDefaults/
COPY Platform.Messaging/ Platform.Messaging/
COPY Platform.Security/ Platform.Security/
COPY Services/Reviews/Reviews.Domain/ Services/Reviews/Reviews.Domain/
COPY Services/Reviews/Reviews.Application/ Services/Reviews/Reviews.Application/
COPY Services/Reviews/Reviews.Infrastructure/ Services/Reviews/Reviews.Infrastructure/
COPY Services/Reviews/Reviews.WebApi/ Services/Reviews/Reviews.WebApi/
RUN dotnet publish Services/Reviews/Reviews.WebApi/Reviews.WebApi.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# 3) середовище виконання: лише ASP.NET runtime, без SDK; не root
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Reviews.WebApi.dll"]
