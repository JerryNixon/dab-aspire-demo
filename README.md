# Data API Builder + .NET Aspire + Razor Pages Sample

A minimal sample showing how .NET Aspire composes SQL Server + Data API Builder (DAB) + a Razor Pages UI. All connection details (including the SQL connection string) are generated and injected by AppHost; you do not need to manually edit them.

## What is here?

Project | Purpose
------- | -------
`AppHost` | Orchestrates SQL Server, Data API Builder container, and Web project with Aspire.
`Database` | SQL project (`.sqlproj`) defining schema and seed script.
`Web` | Razor Pages frontend consuming DAB REST endpoints.
`api/dab-config.json` | Mounted into the DAB container (provided in repo). No manual connection string changes required.

## Quick start

Prerequisites:
- .NET 8 SDK
- Docker Desktop running
- [Microsoft Foundry Local](https://learn.microsoft.com/azure/ai-foundry/foundry-local/get-started) (install with `winget install Microsoft.FoundryLocal --accept-package-agreements --accept-source-agreements`)

Steps:
1. **Prime Foundry Local** (one-time per machine):
   - Start the service if it is not already running: `foundry service start`.
   - Download the model version AppHost expects: `foundry model download qwen2.5-0.5b-instruct-cuda-gpu:3`.
   - Verify the cache shows alias `v3`: `foundry cache list`.
2. Run the app: hit `F5` in VS Code or `dotnet run --project AppHost`.
3. Aspire Workbench opens automatically—inspect resources and use the provided links.
4. Useful endpoints once everything is up:
   - DAB Swagger: `/swagger`
   - GraphQL (if enabled): `/graphql`
   - Health: `/health`
   - Web UI: root of the web project

> ℹ️ AppHost pins the language model deployment to `qwen2.5-0.5b` version **3**. If you accidentally cancel a download, delete the `download.tmp` in `~/.foundry/cache/models/Microsoft/qwen2.5-0.5b-instruct-cuda-gpu-3/` and run `foundry service restart` to make Foundry re-index the cache.

## Data access pattern
The Web project uses repositories (`TodoRepository`, `CategoryRepository`) that talk to DAB via `TableRepository<T>` instances produced by `DabRepositoryFactory`. Environment variables (e.g. `services__dab__http__0`) supplied by Aspire give the DAB base URL at runtime.

## UI
`Pages/Index.cshtml` shows pending vs. completed items. A checkbox toggles completion; icon links handle edit/delete with minimal chrome. `/health` is exposed for quick container readiness checks.

## License
Add a license of your choice (e.g. MIT) at the root.
