<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <RootNamespace>{{App}}.Mcp</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MediatR" />
    <PackageReference Include="ModelContextProtocol.AspNetCore" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../{{App}}.Application/{{App}}.Application.csproj" />
    <ProjectReference Include="../{{App}}.Infrastructure/{{App}}.Infrastructure.csproj" />
  </ItemGroup>

</Project>
