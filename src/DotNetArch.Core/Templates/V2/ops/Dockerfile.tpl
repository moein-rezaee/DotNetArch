# syntax=docker/dockerfile:1
ARG DOTNET_VERSION={{DotnetMajor}}.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
# Restore first (cached layer): only manifests that influence package resolution.
COPY global.json Directory.Build.props Directory.Packages.props {{NuGetConfigCopy}}./
COPY kits/ kits/
COPY src/{{App}}.Domain/{{App}}.Domain.csproj src/{{App}}.Domain/
COPY src/{{App}}.Application/{{App}}.Application.csproj src/{{App}}.Application/
COPY src/{{App}}.Infrastructure/{{App}}.Infrastructure.csproj src/{{App}}.Infrastructure/
COPY src/{{App}}.Api/{{App}}.Api.csproj src/{{App}}.Api/
{{RestoreInstruction}}
COPY src/ src/
RUN dotnet publish src/{{App}}.Api/{{App}}.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app
COPY --from=build /app/publish .
# Writable data folder for SQLite; owned by the non-root app user so named volumes inherit it.
USER root
RUN mkdir -p /data && chown $APP_UID /data
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "{{App}}.Api.dll"]
