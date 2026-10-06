      - step:
          name: Pack and push kits
          script:
            - export NUGET_SOURCE="{{NuGetSource}}"   # NUGET_API_KEY comes from a secured repository variable
            - bash scripts/pack-kits.sh
