<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../../packages/{{Name}}/Corevia.Kit.{{Name}}.Abstractions/Corevia.Kit.{{Name}}.Abstractions.csproj" />
    <!-- dotnet-arch:kit-projects (new projects of the kit are referenced above this line) -->
  </ItemGroup>
</Project>
