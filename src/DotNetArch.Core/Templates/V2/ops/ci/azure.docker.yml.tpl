  - script: |
      echo "$(REGISTRY_PASSWORD)" | docker login {{RegistryHost}} -u "$(REGISTRY_USER)" --password-stdin
      docker build -f src/{{App}}.Api/Dockerfile -t {{ImageName}}:$(Build.SourceVersion) .
      docker push {{ImageName}}:$(Build.SourceVersion)
    displayName: Build and push image
    condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
