<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <PackageId>{{Prefix}}.Kit.{{Area}}.Providers.{{Provider}}</PackageId>
    <RootNamespace>{{Prefix}}.Kit.{{Area}}.Providers.{{Provider}}</RootNamespace>
    <Description>{{Area}} kit provider: {{Provider}}.</Description>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
{{ProviderPackageReferences}}  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="{{Prefix}}.Kit.{{Area}}.Tests" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../{{Prefix}}.Kit.{{Area}}.Abstractions/{{Prefix}}.Kit.{{Area}}.Abstractions.csproj" />
    <ProjectReference Include="../{{Prefix}}.Kit.{{Area}}.Core/{{Prefix}}.Kit.{{Area}}.Core.csproj" />
  </ItemGroup>

</Project>
