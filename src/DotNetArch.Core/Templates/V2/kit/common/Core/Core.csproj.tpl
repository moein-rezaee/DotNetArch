<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <PackageId>{{Prefix}}.Kit.{{Area}}.Core</PackageId>
    <RootNamespace>{{Prefix}}.Kit.{{Area}}.Core</RootNamespace>
    <Description>{{Area}} kit core: options binding, provider selection and the single registration entry point.</Description>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
{{CorePackageReferences}}  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="{{Prefix}}.Kit.{{Area}}.Tests" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../{{Prefix}}.Kit.{{Area}}.Abstractions/{{Prefix}}.Kit.{{Area}}.Abstractions.csproj" />
  </ItemGroup>

</Project>
