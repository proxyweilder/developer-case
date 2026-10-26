FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY NuGet.Config .
COPY src/Case2.Domain/Case2.Domain.csproj src/Case2.Domain/
COPY src/Case2.Api/Case2.Api.csproj src/Case2.Api/
RUN dotnet restore src/Case2.Api/Case2.Api.csproj --configfile NuGet.Config
COPY src/Case2.Domain/ src/Case2.Domain/
COPY src/Case2.Api/ src/Case2.Api/
RUN dotnet publish src/Case2.Api/Case2.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet","Case2.Api.dll"]
