image: mcr.microsoft.com/dotnet/sdk:{{DotnetMajor}}.0

definitions:
  steps:
    - step: &build-test
        name: Build and test
        script:
{{NuGetExport}}          - dotnet tool restore
          - dotnet build --configuration Release
          - dotnet test --configuration Release --no-build --filter "Category!=Integration"
          - bash scripts/validate-examples.sh

pipelines:
  default:
    - step: *build-test
{{DockerJob}}
