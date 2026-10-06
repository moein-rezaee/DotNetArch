
  kits:
    needs: build-test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    env:
      NUGET_SOURCE: {{NuGetSource}}
      NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - run: bash scripts/pack-kits.sh
