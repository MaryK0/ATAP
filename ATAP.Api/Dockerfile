# Образ ATAP.Api. Сборка из корня репозитория:  docker build -f ATAP.Api/Dockerfile -t atap-api .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Сначала только файлы проектов — слой с пакетами NuGet кэшируется между сборками
COPY ATAP.BusinessLogic/ATAP.BusinessLogic.csproj ATAP.BusinessLogic/
COPY ATAP.DataAccess/ATAP.DataAccess.csproj ATAP.DataAccess/
COPY ATAP.Api/ATAP.Api.csproj ATAP.Api/
COPY web_lr2/openapi.yaml web_lr2/openapi.yaml
RUN dotnet restore ATAP.Api/ATAP.Api.csproj

COPY ATAP.BusinessLogic/ ATAP.BusinessLogic/
COPY ATAP.DataAccess/ ATAP.DataAccess/
COPY ATAP.Api/ ATAP.Api/
RUN dotnet publish ATAP.Api/ATAP.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

# Адрес и подключения задаются переменными окружения (ConnectionStrings__DefaultConnection, Jwt__Key, ...)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ATAP.Api.dll"]
