openspec: 1
name: {{App}}
layout: v2
runtime:
  framework: {{Tfm}}
  database: {{Provider}}
layers: [Domain, Application, Infrastructure, Api]
rules:
  dependency_direction: "Api/Mcp -> Infrastructure -> Application -> Domain"
  feature_folder: "Features/<Plural>/{Commands,Queries,Actions,Events,Dtos,Services}"
  configuration: "appsettings = non-sensitive; environment/.env = secrets (UPPER_CASE)"
entities:
  # <dotnet-arch:entities>
kits: []
