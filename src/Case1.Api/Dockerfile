FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY NuGet.Config .
COPY src/Case1.Domain/Case1.Domain.csproj src/Case1.Domain/
COPY src/Case1.Api/Case1.Api.csproj src/Case1.Api/
RUN dotnet restore src/Case1.Api/Case1.Api.csproj --configfile NuGet.Config
COPY src/Case1.Domain/ src/Case1.Domain/
COPY src/Case1.Api/ src/Case1.Api/
RUN dotnet publish src/Case1.Api/Case1.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet","Case1.Api.dll"]
