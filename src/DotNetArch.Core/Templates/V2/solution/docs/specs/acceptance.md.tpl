# {{App}} - acceptance

- A1. `dotnet build -c Release` is warning-free and `dotnet test` passes on a clean clone.
- A2. Each use case answers the status codes listed in `contracts.md`; validation and business-rule failures are problem details, never stack traces.
- A3. The service starts with only `DATABASE_CONNECTION_STRING` set (plus kit secrets for the kits in use); a missing required secret fails at start with a message naming the key.
- A4. `.env.example` and `appsettings.example.json` match the code (`scripts/validate-examples.sh`).
- A5. Layer rules hold: Domain has no references; Application references no Infrastructure, host or kit Core/Provider.
- A6. Every endpoint and MCP tool of an entity runs the same MediatR request (same behaviour over HTTP and MCP).
