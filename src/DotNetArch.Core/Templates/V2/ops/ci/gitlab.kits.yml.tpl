
kits:
  stage: package
  rules:
    - if: $CI_COMMIT_BRANCH == $CI_DEFAULT_BRANCH
  variables:
    NUGET_SOURCE: "{{NuGetSource}}"
  script:
    - bash scripts/pack-kits.sh   # NUGET_API_KEY comes from a masked CI/CD variable
