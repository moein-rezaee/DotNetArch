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
      DATABASE_CONNECTION_STRING: Host=db;Database={{DatabaseName}};Username=postgres;Password=${POSTGRES_PASSWORD:?set POSTGRES_PASSWORD in .env}
    depends_on:
      db:
        condition: service_healthy

  db:
    image: postgres:16
    environment:
      POSTGRES_DB: {{DatabaseName}}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?set POSTGRES_PASSWORD in .env}
    volumes:
      - dbdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres -d {{DatabaseName}}"]
      interval: 5s
      timeout: 5s
      retries: 10

volumes:
  dbdata:
