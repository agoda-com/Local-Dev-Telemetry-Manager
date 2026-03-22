# Agoda.DevExTelemetry

A full-stack developer experience telemetry dashboard that collects, stores, and visualizes build and test metrics from development tools.

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

## Ingest Endpoints

| Endpoint | Payload Type |
|---|---|
| `POST /dotnet` | .NET MSBuild compile, ASP.NET startup, first response |
| `POST /dotnet/nunit` | NUnit test results |
| `POST /junit` | JUnit test results |
| `POST /jest` | Jest test results |
| `POST /vitest` | Vitest test results |
| `POST /scala/scalatest` | ScalaTest results (gzip supported) |
| `POST /webpack` | Webpack build metrics |
| `POST /vite` | Vite build metrics |
| `POST /gradletalaiot` | Gradle Talaiot build metrics |
| `POST /testdata/junit` | JUnit XML multipart upload |

## Dashboard Pages

- **Test Run Performance** — pass rates, durations, per-test-case drill-down
- **API Build Performance** — compile, startup, and first response times
- **Clientside Build Performance** — hot reload vs full build metrics

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
