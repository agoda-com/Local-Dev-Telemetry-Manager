# Agoda.DevExTelemetry

A full-stack developer experience telemetry dashboard that collects, stores, and visualizes build and test metrics from development tools.

This is an internal tool designed to be deployed within your organization's infrastructure. It receives telemetry from build plugins and test runners across your engineering teams, providing visibility into compile times, test pass rates, hot reload performance, and other developer productivity signals. It is not intended to be exposed to the public internet.

## Architecture

- **Backend:** .NET 10, ASP.NET Core (Kestrel), EF Core with SQLite
- **Frontend:** React 19, TypeScript, Vite 8, Tailwind CSS 3, Tremor, Recharts
- **CI/CD:** GitHub Actions, Azure App Service (Linux)

## Project Structure

```
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

## Client Configuration

The telemetry clients (devfeedback NuGet packages, webpack/vite plugins, test reporter plugins) need to know where to send data. There are two ways to point them at your deployment:

### Option 1: Environment Variable Override

Set the `DEVFEEDBACK_URL` environment variable on developer machines to your server's URL:

```bash
export DEVFEEDBACK_URL=https://your-devex-telemetry.azurewebsites.net
```

This overrides the default URL in all compilation metrics clients. You can set this in shell profiles, dotfiles, or machine-level environment variables.

### Option 2: Internal DNS

If you control your organization's DNS, you can create a DNS record that resolves the default hostname used by the compilation metrics clients to your deployment's IP. This way clients work without any local configuration — traffic is routed transparently via your internal DNS server.

This approach is preferable for large teams since it requires zero setup on individual developer machines.

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

83 tests total (21 unit + 62 integration).

## Deployment

Pushes to `main` trigger automatic deployment to Azure App Service via GitHub Actions.

| Workflow | Trigger | Purpose |
|---|---|---|
| `build.yml` | Pull requests to main | Build + test validation |
| `deploy.yml` | Push to main | Build + deploy to Azure |
