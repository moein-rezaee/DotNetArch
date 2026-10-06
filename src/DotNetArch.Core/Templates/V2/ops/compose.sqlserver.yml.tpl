services:
  api:
    build:
      context: .
      dockerfile: src/{{App}}.Api/Dockerfile
    image: {{ImageName}}:${APP_VERSION:-0.0.0}
    container_name: {{ContainerName}}
    ports:
      - "{{Port}}:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Production}
      Database__MigrateOnStartup: "true"
      DATABASE_CONNECTION_STRING: Server=db,1433;Database={{DatabaseName}};User Id=sa;Password=${SQLSERVER_PASSWORD:?set SQLSERVER_PASSWORD in .env};TrustServerCertificate=True
    depends_on:
      db:
        condition: service_healthy

  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: ${SQLSERVER_PASSWORD:?set SQLSERVER_PASSWORD in .env}
    volumes:
      - dbdata:/var/opt/mssql
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P \"$$MSSQL_SA_PASSWORD\" -Q \"SELECT 1\" || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 10

volumes:
  dbdata:
