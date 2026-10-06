# syntax=docker/dockerfile:1.7
#
# Build context: the corevia-identity repository root.
# Corevia.Kit.* is consumed as published PackageReferences restored from the Nexus NuGet feed
# (see NuGet.config); no sibling corevia-kit checkout or named build context is required.
#
#   docker buildx build \
#     --secret id=nuget_config,src=<nuget.config with credentials> \
#     -f IdentityService.Api/Dockerfile -t <image> .
ARG DOCKER_REGISTRY=nexus.getcorevia.ir:8083
FROM ${DOCKER_REGISTRY}/dotnet/sdk:9.0 AS build
ARG NUGET_CERT_REVOCATION_MODE=offline
ENV NUGET_CERT_REVOCATION_MODE=${NUGET_CERT_REVOCATION_MODE}
WORKDIR /src/corevia-identity
COPY NuGet.config global.json Directory.Build.targets ./
COPY IdentityService.Api/IdentityService.Api.csproj IdentityService.Api/
COPY IdentityService.Application/IdentityService.Application.csproj IdentityService.Application/
COPY IdentityService.Domain/IdentityService.Domain.csproj IdentityService.Domain/
COPY IdentityService.Infrastructure/IdentityService.Infrastructure.csproj IdentityService.Infrastructure/
COPY IdentityService.Infrastructure.Database/IdentityService.Infrastructure.Database.csproj IdentityService.Infrastructure.Database/

RUN --mount=type=secret,id=nuget_config,required=true \
    dotnet restore IdentityService.Api/IdentityService.Api.csproj --configfile /run/secrets/nuget_config

COPY . .
RUN --mount=type=secret,id=nuget_config,required=true \
    dotnet publish IdentityService.Api/IdentityService.Api.csproj -c Release --no-restore -o /app/publish

FROM ${DOCKER_REGISTRY}/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5270
ENV ASPNETCORE_URLS=http://+:5270
ENTRYPOINT ["dotnet", "IdentityService.Api.dll"]
