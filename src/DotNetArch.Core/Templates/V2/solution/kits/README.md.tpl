# kits

Independent, provider-based packages for external capabilities (cache, message broker, media storage, ...).
Each area has its own folder with `Abstractions`, `Core` and one `Providers.<Name>` package, its own version, docs and specs.

```bash
dotnet-arch new kit --area Cache --providers Redis,InMemory   # generate and wire into this solution
dotnet-arch add kit MessageBroker                              # wire an existing kit
```
Services depend on a kit's Abstractions only (Application layer); Core and Providers are referenced by the Api composition root.
`scripts/pack-kits.sh` packs every kit and pushes them to the configured NuGet feed.
