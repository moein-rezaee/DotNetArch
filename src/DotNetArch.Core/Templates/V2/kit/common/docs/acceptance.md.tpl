# {{Area}} kit - acceptance

- A1. `dotnet build -c Release` succeeds with no warnings; the kit builds without any parent repository.
- A2. With one provider registered, `Add{{Area}}Kit` works without `{{Area}}:Provider`; with several, a missing key fails naming `{{Area}}:Provider`.
- A3. An unknown provider name fails and lists the registered providers.
- A4. Abstractions has no third-party package references; Core references no provider package.
- A5. No secret appears in appsettings or in this repository; every secret key is documented in the README.
- A6. `scripts/pack.sh` produces one nupkg per package with the kit's version.
