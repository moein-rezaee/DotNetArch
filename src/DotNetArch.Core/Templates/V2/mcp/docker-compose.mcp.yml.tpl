# Adds the MCP host next to the Api:  docker compose -f docker-compose.yml -f docker-compose.mcp.yml up
services:
  mcp:
    build:
      context: .
      dockerfile: src/{{App}}.Mcp/Dockerfile
    image: {{ImageName}}-mcp:${APP_VERSION:-0.0.0}
    container_name: {{ContainerName}}-mcp
    ports:
      - "{{McpPort}}:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Production}
      DATABASE_CONNECTION_STRING: {{ComposeConnectionString}}
      MCP_AUTH_TOKEN: ${MCP_AUTH_TOKEN:?set MCP_AUTH_TOKEN in .env}
{{ComposeMcpExtras}}
