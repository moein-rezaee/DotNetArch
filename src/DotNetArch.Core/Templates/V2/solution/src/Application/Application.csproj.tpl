<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>{{App}}.Application</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MediatR" />
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="{{App}}.Application.Tests" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../{{App}}.Domain/{{App}}.Domain.csproj" />
  </ItemGroup>

</Project>
