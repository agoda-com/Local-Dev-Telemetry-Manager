<p align="center">
  <img src="src/Clientside/src/DevExSml.png" alt="DX Telemetry logo" width="120" />
</p>

# DX Telemetry Manager

A full-stack **DX telemetry** dashboard that collects, stores, and visualizes build and test metrics from development tools.

Logo concepts: [`agoda_devex_telemetry_logo_concepts.html`](agoda_devex_telemetry_logo_concepts.html)

This is an internal tool designed to be deployed within your organization's infrastructure. It receives telemetry from build plugins and test runners across your engineering teams, providing visibility into compile times, test pass rates, hot reload performance, and other developer productivity signals. It is **not** intended to be exposed to the public internet.

## Architecture

- **Backend:** .NET 10, ASP.NET Core (Kestrel), EF Core with SQLite or PostgreSQL
- **Frontend:** React 19, TypeScript, Vite 8, Tailwind CSS 3, Tremor, Recharts
- **Container:** Docker image available at `agoda/devex-telemetry`

## Project Structure

```text
src/
├── Agoda.DevExTelemetry.WebApi/        # ASP.NET Core API (controllers, Program.cs)
├── Agoda.DevExTelemetry.Core/          # Domain layer (entities, DTOs, services, DbContext)
├── Agoda.DevExTelemetry.IntegrationTests/
├── Agoda.DevExTelemetry.UnitTests/
├── Clientside/                         # React SPA (Vite + Tailwind + Tremor)
└── Agoda.DevExTelemetry.sln
```

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
| Talaiot Gradle plugin | Gradle | `POST /gradletalaiot` | [Talaiot](https://github.com/cdsap/Talaiot) (external) |
| Jest reporter | npm | `POST /jest` | [testresults-collector](https://github.com/agoda-com/testresults-collector) |
| Vitest reporter | npm | `POST /vitest` | [testresults-collector](https://github.com/agoda-com/testresults-collector) |

## Ingest Endpoints

| Endpoint | Payload Type |
|---|---|
| `POST /dotnet` | .NET MSBuild compile, ASP.NET startup, first response |
| `POST /dotnet/nunit` | NUnit / xUnit test results |
| `POST /junit` | JUnit test results |
| `POST /jest` | Jest test results |
| `POST /vitest` | Vitest test results |
| `POST /scala/scalatest` | ScalaTest results (gzip supported) |
| `POST /webpack` | Webpack build metrics |
| `POST /vite` | Vite build / HMR metrics |
| `POST /gradletalaiot` | Gradle Talaiot build metrics |
| `POST /testdata/junit` | JUnit XML multipart upload |

## Dashboard Pages

- **Test Run Performance** — pass rates, durations, per-test-case drill-down
- **API Build Performance** — compile, startup, and first response times
- **Clientside Build Performance** — hot reload vs full build metrics

## PostgreSQL Support

The API supports PostgreSQL when `POSTGRES_CONNECTION_STRING` is provided.

```bash
export POSTGRES_CONNECTION_STRING='Host=localhost;Port=5432;Database=devex_telemetry;Username=devex;Password=devex'
```

Notes:
- If `POSTGRES_CONNECTION_STRING` is not set, the app uses SQLite.
- Current PostgreSQL initialization uses `EnsureCreated()` as an MVP path.
- For long-term schema evolution, move to dedicated PostgreSQL migrations and `db.Database.Migrate()`.

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

### Running Tests

```bash
cd src
dotnet test
```

## CI/CD

Pushes to `main` trigger automatic deployment to Azure App Service via GitHub Actions.

| Workflow | Trigger | Purpose |
|---|---|---|
| `build.yml` | Pull requests to main | Build + test validation |
| `deploy.yml` | Push to main | Build + deploy to Azure |
