      - step:
          name: Build and push image
          services: [docker]
          script:
            - echo "$REGISTRY_PASSWORD" | docker login {{RegistryHost}} -u "$REGISTRY_USER" --password-stdin
            - docker build -f src/{{App}}.Api/Dockerfile -t {{ImageName}}:$BITBUCKET_COMMIT .
            - docker push {{ImageName}}:$BITBUCKET_COMMIT
