trigger:
  branches:
    include: [main, develop]

pool:
  vmImage: ubuntu-latest

variables:
  DOTNET_NOLOGO: "true"
  DOTNET_CLI_TELEMETRY_OPTOUT: "true"
{{NuGetEnv}}
steps:
  - task: UseDotNet@2
    inputs:
      useGlobalJson: true
  - script: dotnet tool restore
    displayName: Restore tools
  - script: dotnet build --configuration Release
    displayName: Build
  - script: dotnet test --configuration Release --no-build --filter "Category!=Integration"
    displayName: Test
  - script: bash scripts/validate-examples.sh
    displayName: Validate configuration examples
{{DockerJob}}{{KitsJob}}
