<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <PackageId>Corevia.Kit.{{Name}}.Providers.{{Provider}}</PackageId>
    <RootNamespace>Corevia.Kit.{{Name}}.Providers.{{Provider}}</RootNamespace>
    <AssemblyName>Corevia.Kit.{{Name}}.Providers.{{Provider}}</AssemblyName>
    <Version>1.0.0</Version>
    <PackageVersion>1.0.0</PackageVersion>
    <Authors>Platform Engineering</Authors>
    <Description>{{Description}}</Description>
    <RepositoryUrl>https://gitlab.getcorevia.ir/corevia/corevia-kit</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageProjectUrl>https://gitlab.getcorevia.ir/corevia/corevia-kit</PackageProjectUrl>
    <PackageLicenseFile>LICENSE.txt</PackageLicenseFile>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageRequireLicenseAcceptance>false</PackageRequireLicenseAcceptance>
    <PackageTags>corevia;kit;shared-runtime;provider</PackageTags>
  </PropertyGroup>
  <ItemGroup>
    <None Include="../../../LICENSE.txt" Pack="true" PackagePath="/" />
    <None Include="../../../README.md" Pack="true" PackagePath="/" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="**/*.cs" Exclude="bin/**;obj/**" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../Corevia.Kit.{{Name}}.Abstractions/Corevia.Kit.{{Name}}.Abstractions.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="9.0.0" />
  </ItemGroup>
</Project>
