<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <RootNamespace>{{App}}.Api</RootNamespace>
    <Version>1.0.0</Version>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MediatR" />
    <PackageReference Include="Swashbuckle.AspNetCore" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../{{App}}.Application/{{App}}.Application.csproj" />
    <ProjectReference Include="../{{App}}.Infrastructure/{{App}}.Infrastructure.csproj" />
  </ItemGroup>

</Project>
