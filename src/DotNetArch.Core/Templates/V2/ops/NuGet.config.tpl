<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="{{NuGetSourceName}}" value="{{NuGetSource}}" />
  </packageSources>
  <!--
    Credentials are never stored here. Provide them through the environment variable
    NuGetPackageSourceCredentials_{{NuGetSourceEnvName}}="Username=...;Password=..." (CI secret) or `dotnet nuget update source`.
  -->
</configuration>
