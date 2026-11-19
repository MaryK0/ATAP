FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ATAP.BusinessLogic/ATAP.BusinessLogic.csproj ATAP.BusinessLogic/
COPY ATAP.DataAccess/ATAP.DataAccess.csproj ATAP.DataAccess/
COPY ATAP.BlazorUI/ATAP.BlazorUI.csproj ATAP.BlazorUI/
RUN dotnet restore ATAP.BlazorUI/ATAP.BlazorUI.csproj
COPY ATAP.BusinessLogic/ ATAP.BusinessLogic/
COPY ATAP.DataAccess/ ATAP.DataAccess/
COPY ATAP.BlazorUI/ ATAP.BlazorUI/
RUN dotnet publish ATAP.BlazorUI/ATAP.BlazorUI.csproj -c Release -o /app --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ATAP.BlazorUI.dll"]
