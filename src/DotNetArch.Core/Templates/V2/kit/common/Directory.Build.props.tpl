<Project>

  <!-- This kit is independent: it has its own build settings and version and does not inherit from any parent repository. -->
  <PropertyGroup>
    <TargetFramework>{{Tfm}}</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest</AnalysisLevel>
    <Deterministic>true</Deterministic>
    <Version>1.0.0</Version>
    <Authors>{{Authors}}</Authors>
    <PackageTags>{{PackageTags}}</PackageTags>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>

  <PropertyGroup Condition="'$(CI)' == 'true' or '$(Configuration)' == 'Release'">
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup Condition="'$(IsPackable)' != 'false'">
    <None Include="$(MSBuildThisFileDirectory)README.md" Pack="true" PackagePath="/" Visible="false" />
  </ItemGroup>

</Project>
