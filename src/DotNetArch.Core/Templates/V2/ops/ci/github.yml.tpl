name: CI

on:
  push:
    branches: [main, develop]
  pull_request:

permissions:
  contents: read

env:
  DOTNET_NOLOGO: "true"
  DOTNET_CLI_TELEMETRY_OPTOUT: "true"
{{NuGetEnv}}
jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - run: dotnet tool restore
      - run: dotnet build --configuration Release
      - run: dotnet test --configuration Release --no-build --filter "Category!=Integration"
      - run: bash scripts/validate-examples.sh
{{DockerJob}}{{KitsJob}}
