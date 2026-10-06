<Project>

  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <PackageVersion Include="Microsoft.Extensions.Configuration.Abstractions" Version="{{ExtConfigurationVersion}}" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Binder" Version="{{ExtConfigurationVersion}}" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="{{ExtDependencyInjectionVersion}}" />
{{KitPackageVersions}}  </ItemGroup>

  <ItemGroup Label="Tests">
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="{{ExtDependencyInjectionImplVersion}}" />
    <PackageVersion Include="Microsoft.Extensions.Configuration" Version="{{ExtConfigurationVersion}}" />
  </ItemGroup>

</Project>
