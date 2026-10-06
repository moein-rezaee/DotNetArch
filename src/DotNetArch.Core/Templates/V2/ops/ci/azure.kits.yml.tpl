  - script: bash scripts/pack-kits.sh
    displayName: Pack and push kits
    env:
      NUGET_SOURCE: {{NuGetSource}}
      NUGET_API_KEY: $(NUGET_API_KEY)
    condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
