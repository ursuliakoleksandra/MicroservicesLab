# Збірка з КОРЕНЯ репозиторію:  docker build -f Services/Catalog/Catalog.Api/Dockerfile -t technest-catalog-api .
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY Services/Catalog/Catalog.Domain/Catalog.Domain.csproj Services/Catalog/Catalog.Domain/
COPY Services/Catalog/Catalog.Dal/Catalog.Dal.csproj Services/Catalog/Catalog.Dal/
COPY Services/Catalog/Catalog.Bll/Catalog.Bll.csproj Services/Catalog/Catalog.Bll/
COPY Services/Catalog/Catalog.Api/Catalog.Api.csproj Services/Catalog/Catalog.Api/
RUN dotnet restore Services/Catalog/Catalog.Api/Catalog.Api.csproj
COPY Services/Catalog/ Services/Catalog/
RUN dotnet publish Services/Catalog/Catalog.Api/Catalog.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# рядок підключення: -e ConnectionStrings__CatalogDb="Server=...;Database=CatalogDb;..."
ENTRYPOINT ["dotnet", "Catalog.Api.dll"]
