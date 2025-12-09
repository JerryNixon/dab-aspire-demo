# Data API Builder + .NET Aspire + Razor Pages Sample

A minimal sample showing how .NET Aspire composes SQL Server + Data API Builder (DAB) + a Razor Pages UI. AppHost wires resources and injects configuration at runtime.

## What is here?

Project | Purpose
------- | -------
`AppHost` | Orchestrates SQL Server, Data API Builder container, Redis, MCP Inspector, and the Web project with Aspire.
`Database` | SQL project (`.sqlproj`) defining schema and seed script.
`Web` | Razor Pages frontend consuming DAB endpoints and Azure AI Foundry.
`api/dab-config.json` | Mounted into the DAB container (provided in repo). No manual connection string edits required.

## Prerequisites
- .NET 8 SDK
- Docker Desktop running
- Azure AI Foundry API key (for chat)

## Quick start
1. Restore and build:
   - `dotnet restore`
   - `dotnet build`
2. Run the Aspire host:
   - `dotnet run --project AppHost`
3. When prompted or via env vars, provide:
   - `ASPIRE_PARAMETER_sql-password`
   - `ASPIRE_PARAMETER_azure-aifoundry-apikey`
4. Open the Aspire Workbench links:
   - Web UI: `web-app`
   - DAB: `/swagger`, `/graphql`, `/health`
   - Redis Commander (optional): root
   - MCP Inspector (developer tool)

## Notes
- SQL data volume is optional and can be enabled in `AppHost/AppHost.cs`.
- OTLP exporter can be configured; set `OTEL_EXPORTER_OTLP_ENDPOINT` if using Aspire Dashboard.
- DAB config bind-mount path: `api/dab-config.json`.

## License
Add a license (e.g., MIT) at the root.
