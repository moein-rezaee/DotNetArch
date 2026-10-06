stages: [build, test, package]

variables:
  DOTNET_NOLOGO: "true"
  DOTNET_CLI_TELEMETRY_OPTOUT: "true"
{{NuGetEnv}}
default:
  image: mcr.microsoft.com/dotnet/sdk:{{DotnetMajor}}.0

build:
  stage: build
  script:
    - dotnet tool restore
    - dotnet build --configuration Release

test:
  stage: test
  script:
    - dotnet test --configuration Release --filter "Category!=Integration"
    - bash scripts/validate-examples.sh
{{DockerJob}}{{KitsJob}}
