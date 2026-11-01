<p align="center">
  <img src="docs/assets/dx-telemetry-logo-header.svg" alt="DX Telemetry Manager logo" width="720" />
</p>

# DX Telemetry Manager

A full-stack **DX telemetry** dashboard that collects, stores, and visualizes build and test metrics from development tools.

This is an internal tool designed to be deployed within your organization's infrastructure. It receives telemetry from build plugins and test runners across your engineering teams, providing visibility into compile times, test pass rates, hot reload performance, and other developer productivity signals. It is **not** intended to be exposed to the public internet.

## Architecture

- **Backend:** .NET 10, ASP.NET Core (Kestrel), EF Core with SQLite or PostgreSQL
- **Frontend:** React 19, TypeScript, Vite 8, Tailwind CSS 3, Tremor, Recharts
- **Container:** Docker image available at `agoda/devex-telemetry`

## Telemetry Clients

These are the client libraries that instrument developer tooling and send telemetry to this server:

| Client Package | Install | Sends To | Source |
|---|---|---|---|
| `Agoda.Builds.Metrics` | [NuGet](https://www.nuget.org/packages/Agoda.Builds.Metrics) | `POST /dotnet` | [dotnet-build-metrics](https://github.com/agoda-com/dotnet-build-metrics) |
| `Agoda.DevFeedback.AspNetStartup` | NuGet | `POST /dotnet` | [dotnet-build-metrics](https://github.com/agoda-com/dotnet-build-metrics) |
| `Agoda.Tests.Metrics.NUnit` | NuGet | `POST /dotnet/nunit` | [dotnet-build-metrics](https://github.com/agoda-com/dotnet-build-metrics) |
| `Agoda.Tests.Metrics.xUnit` | NuGet | `POST /dotnet/nunit` | [dotnet-build-metrics](https://github.com/agoda-com/dotnet-build-metrics) |
| `agoda-devfeedback-webpack` | npm | `POST /webpack` | [devfeedback-js](https://github.com/agoda-com/devfeedback-js) |
| `agoda-devfeedback-vite2` | npm | `POST /vite` | [devfeedback-js](https://github.com/agoda-com/devfeedback-js) |
| `agoda-devfeedback-rsbuild` | npm | `POST /vite` | [devfeedback-js](https://github.com/agoda-com/devfeedback-js) |
| JUnit reporter | Maven | `POST /junit` | [java-local-metrics](https://github.com/agoda-com/java-local-metrics) |
| ScalaTest reporter | sbt | `POST /scala/scalatest` | [java-local-metrics](https://github.com/agoda-com/java-local-metrics) |
| Gradle build metrics plugin | Gradle | `POST /gradle` | [kotlin-local-metrics](https://github.com/agoda-com/kotlin-local-metrics) |
| Ktor startup metrics plugin | Gradle | `POST /ktor` | [kotlin-local-metrics](https://github.com/agoda-com/kotlin-local-metrics) |
| Talaiot Gradle plugin | Gradle | `POST /gradletalaiot` | [Talaiot](https://github.com/cdsap/Talaiot) (external) |
| Jest reporter | npm | `POST /jest` | [testresults-collector](https://github.com/agoda-com/testresults-collector) |
| Vitest reporter | npm | `POST /vitest` | [testresults-collector](https://github.com/agoda-com/testresults-collector) |

## PostgreSQL Support

The API supports PostgreSQL when `POSTGRES_CONNECTION_STRING` is provided.

```bash
export POSTGRES_CONNECTION_STRING='Host=localhost;Port=5432;Database=devex_telemetry;Username=devex;Password=devex'
```

Notes:
- If `POSTGRES_CONNECTION_STRING` is not set, the app uses SQLite.

## Docker Usage

### Run from Docker Hub image

```bash
docker run --rm -p 8080:8080 \
  -e POSTGRES_CONNECTION_STRING='Host=host.docker.internal;Port=5432;Database=devex_telemetry;Username=devex;Password=devex' \
  agoda/devex-telemetry:latest
```

### Run with included docker-compose (API + PostgreSQL)

```bash
docker compose up -d
```

This starts:
- API on `http://localhost:8080`
- PostgreSQL on `localhost:5432`

## Pointing Clients at Your Deployment

Two options, depending on how much per-machine configuration you want.

### Option 1 (preferred for larger teams): Internal DNS

The compilation clients default to `http://compilation-metrics`.

Create an internal DNS record so `compilation-metrics` resolves to wherever you host this API. This gives zero per-machine setup: engineers don’t configure anything, telemetry just flows.

### Option 2: Environment variable override per machine

Set `DEVFEEDBACK_URL` on workstations:

```bash
export DEVFEEDBACK_URL=https://your-devex-telemetry.example.com
```

This is useful when DNS changes aren’t available yet. Your IT support team can roll this out centrally via endpoint management.

## Deployment Scenarios and Diagrams

See [docs/deployment-scenarios.md](docs/deployment-scenarios.md) for concrete deployment topologies, client routing examples, and markdown diagrams.

## Using with AI agents

The server exposes an [MCP](https://modelcontextprotocol.io) endpoint at `/mcp`, so coding agents can query the telemetry and investigate slow tests, flaky tests and slow builds themselves. It is read-only, needs no auth (like the rest of the API), and can be turned off with `Mcp:Enabled=false`.

Connect from Claude Code:

```bash
claude mcp add --transport http dx-telemetry http://compilation-metrics/mcp
```

Connect from Cursor by adding this to `.cursor/mcp.json`:

```json
{ "mcpServers": { "dx-telemetry": { "url": "http://compilation-metrics/mcp" } } }
```

### Two layers of tools

| Layer | Tools | Use it to |
|---|---|---|
| Analysis | `get_overview`, `find_slow_tests`, `find_flaky_tests`, `find_failing_tests`, `find_slow_builds`, `explain_build_time`, `find_regressions` | Get a ranked answer to a common question in one call. Each row has a `reason`, and an `evidence` column naming the data tool call that backs it up. Every threshold (`min_runs`, `min_increase_pct` and so on) is a parameter |
| Data | `list_scopes`, `list_test_suites`, `get_tests`, `get_test`, `get_test_failures`, `list_test_runs`, `get_test_run`, `list_build_series`, `get_builds`, `get_build_stats`, `get_build` | Filter, group, sort and page without judging. Check an analysis result, or ask a question the analysis tools don't cover |

There are also four prompts (recipes): `investigate_slow_tests`, `investigate_flaky_tests`, `investigate_build_regressions` and `triage_dev_loop`.

Example questions:
- "Which tests in web-app cost us the most time, and why are they slow?"
- "Which tests are flaky in CI, and do their failures share a cause?"
- "Did the .Net compile get slower this week? For whom?"

### Output and exports

Results are CSV (or `format=json`), capped at 500 rows per call (`Mcp:MaxRows`), with a `next_cursor` for the next page. Durations are whole milliseconds and timestamps are UTC. User names and hostnames are never returned.

Every result also carries an `export_url`. It is `GET /api/export/{tool}.csv` with the same arguments, with no row cap below `Export:MaxRows` (1,000,000). Agents can pull a whole dataset to disk and analyse it there:

```bash
curl -s "http://compilation-metrics/api/export/get_tests.csv?suite_id=ab12cd34&environment=ci&days=30" -o tests.csv
duckdb -c "select test_key, fail_rate, status_sequence from 'tests.csv' where failed > 0 and passed > 0 order by fail_rate desc"
```

`export_url` uses `Mcp:PublicBaseUrl` when it is set. Otherwise it uses the request's host, honouring `X-Forwarded-Proto`/`X-Forwarded-Host` (only for this URL).

## Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)

### Backend

```bash
cd src
dotnet restore
dotnet build
dotnet run --project Agoda.DevExTelemetry.WebApi
```

The API starts at `https://localhost:5001` with Swagger UI at the root.

### Frontend

```bash
cd src/Clientside
npm install --legacy-peer-deps
npm run dev
```

The dev server starts at `http://localhost:5173` and proxies `/api` requests to the backend.

I wrote more about this exact setup in [Bridging Worlds: Making .NET BFF and React/Vite Play Nice in Development](https://medium.com/beer-and-servers-dont-mix/bridging-worlds-making-net-bff-and-react-vite-play-nice-in-development-b0f7b9311790).

### Running Tests

```bash
cd src
dotnet test
```

The MCP tools are tested in `Agoda.DevExTelemetry.McpTests`, which drives `/mcp` with the MCP C# client on SQLite and PostgreSQL. The PostgreSQL fixtures start a Testcontainers container. Without Docker, set `DEVEX_TEST_POSTGRES` to the connection string of a running server, and each test creates and drops its own database there.
