
docker:
  stage: package
  image: docker:27
  services: [docker:27-dind]
  rules:
    - if: $CI_COMMIT_BRANCH == $CI_DEFAULT_BRANCH
  script:
    - echo "$REGISTRY_PASSWORD" | docker login {{RegistryHost}} -u "$REGISTRY_USER" --password-stdin
    - docker build -f src/{{App}}.Api/Dockerfile -t {{ImageName}}:$CI_COMMIT_SHORT_SHA .
    - docker push {{ImageName}}:$CI_COMMIT_SHORT_SHA
