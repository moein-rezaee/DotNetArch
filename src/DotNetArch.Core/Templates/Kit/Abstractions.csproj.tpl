<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <PackageId>Corevia.Kit.{{Name}}.Abstractions</PackageId>
    <RootNamespace>Corevia.Kit.{{Name}}</RootNamespace>
    <AssemblyName>Corevia.Kit.{{Name}}.Abstractions</AssemblyName>
    <Version>1.0.0</Version>
    <PackageVersion>1.0.0</PackageVersion>
    <Authors>Platform Engineering</Authors>
    <Description>{{Description}} (ports and data contracts)</Description>
    <RepositoryUrl>https://gitlab.getcorevia.ir/corevia/corevia-kit</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageProjectUrl>https://gitlab.getcorevia.ir/corevia/corevia-kit</PackageProjectUrl>
    <PackageLicenseFile>LICENSE.txt</PackageLicenseFile>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageRequireLicenseAcceptance>false</PackageRequireLicenseAcceptance>
    <PackageTags>corevia;kit;shared-runtime</PackageTags>
  </PropertyGroup>
  <ItemGroup>
    <None Include="../../../LICENSE.txt" Pack="true" PackagePath="/" />
    <None Include="../../../README.md" Pack="true" PackagePath="/" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="**/*.cs" Exclude="bin/**;obj/**" />
  </ItemGroup>
</Project>
