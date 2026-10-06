<Project>

  <!-- Single source of truth for NuGet versions. Projects reference packages without a version. -->
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup Label="Application">
    <PackageVersion Include="MediatR" Version="{{MediatRVersion}}" />
    <PackageVersion Include="FluentValidation" Version="{{FluentValidationVersion}}" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="{{FluentValidationVersion}}" />
  </ItemGroup>

  <ItemGroup Label="Infrastructure">
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="{{EfCoreVersion}}" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Relational" Version="{{EfCoreVersion}}" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="{{EfCoreVersion}}" />
    <PackageVersion Include="{{EfProviderPackageId}}" Version="{{EfProviderPackageVersion}}" />
  </ItemGroup>

  <ItemGroup Label="Microsoft.Extensions">
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="{{ExtDependencyInjectionVersion}}" />
    <PackageVersion Include="Microsoft.Extensions.Configuration" Version="{{ExtOptionsConfigurationVersion}}" />
    <PackageVersion Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="{{ExtOptionsConfigurationVersion}}" />
    <PackageVersion Include="Microsoft.Extensions.Hosting.Abstractions" Version="{{ExtHostingAbstractionsVersion}}" />
  </ItemGroup>

  <ItemGroup Label="Api">
    <PackageVersion Include="Swashbuckle.AspNetCore" Version="{{SwashbuckleVersion}}" />
  </ItemGroup>

  <ItemGroup Label="Tests">
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
    <PackageVersion Include="NSubstitute" Version="5.3.0" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="{{AspNetVersion}}" />
{{TestSqlitePackageLine}}  </ItemGroup>

</Project>
