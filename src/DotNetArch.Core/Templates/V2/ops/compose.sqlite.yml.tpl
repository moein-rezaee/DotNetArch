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
      DATABASE_CONNECTION_STRING: Data Source=/data/app.db
    volumes:
      - data:/data

volumes:
  data:
