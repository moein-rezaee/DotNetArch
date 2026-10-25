<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <RootNamespace>{{App}}.Cli</RootNamespace>
    <AssemblyName>{{CliName}}</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MediatR" />
    <PackageReference Include="Microsoft.Extensions.Hosting" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../{{App}}.Application/{{App}}.Application.csproj" />
    <ProjectReference Include="../{{App}}.Infrastructure/{{App}}.Infrastructure.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.json" CopyToOutputDirectory="PreserveNewest" />
    <None Update="appsettings.*.json" CopyToOutputDirectory="PreserveNewest" DependentUpon="appsettings.json" />
    <!-- Local development values only; real deployments use environment variables. -->
    <None Include=".env" Condition="Exists('.env')" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
