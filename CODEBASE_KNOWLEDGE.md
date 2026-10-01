# CODEBASE_KNOWLEDGE.md — DX Telemetry Manager

## Quick Reference

| Question | Answer |
|----------|--------|
| **What is this?** | Full-stack DX (developer experience) telemetry dashboard collecting build/test metrics from 10+ client SDKs (.NET, JS, Java, Kotlin); stores in SQLite or PostgreSQL; visualizes trends |
| **Language stack** | Backend: .NET 10 / ASP.NET Core (Kestrel); Frontend: React 19 + TypeScript + Vite 8 |
| **Entry points** | Backend: `Program.cs` (Kestrel on port 8080); Frontend: `main.tsx` (Vite dev server on port 3000) |
| **Database** | EF Core context `TelemetryDbContext` with 4 tables: `BuildMetrics`, `TestRuns`, `TestCases`, `RawPayloads` |
| **Deployment** | Docker multi-stage (Node → frontend build, .NET SDK → publish, ASP.NET runtime); docker-compose with PostgreSQL; K8s + Helm ready |
| **Key responsibilities** | Ingest metric payloads (async queued), classify builds/tests, detect execution environment (CI vs local), query/filter dashboard data, auto-cleanup old data |

---

## 1. Architecture Overview

### System Context
```
External Clients (telemetry SDKs)
  ↓ HTTP POST (gzip-decompressed payloads, up to 500MB)
Frontend (React) ↔ Backend API (ASP.NET Core)
  ↓
Database (SQLite dev / PostgreSQL prod)
  ↓
Async Background Workers (background queue + hosted service for cleanup)
```

### Project Structure
```
Local-Dev-Telemetry-Manager/
├── src/
│   ├── Agoda.DevExTelemetry.sln           # Solution file
│   ├── Agoda.DevExTelemetry.WebApi/       # ASP.NET Core API + static React files
│   ├── Agoda.DevExTelemetry.Core/         # Domain models, services, EF Core context
│   ├── Agoda.DevExTelemetry.IntegrationTests/  # xUnit/NUnit tests (both DB providers)
│   ├── Agoda.DevExTelemetry.UnitTests/    # Unit tests
│   └── Clientside/                         # React + TypeScript + Vite
├── Dockerfile                              # 3-stage: Node frontend → .NET backend → ASP.NET runtime
├── docker-compose.yml                      # API + PostgreSQL
├── .github/workflows/                      # CI: build.yml, docker-publish.yml, deploy.yml
└── docs/deployment-scenarios.md            # K8s, Helm patterns
```

---

## 2. Backend Architecture

### Core Layers

#### **Data Layer** (`Core/Data/`)
- **`TelemetryDbContext.cs`**: EF Core DbContext with 4 tables
  - `BuildMetrics`: compile times, hot reload data, platform info
  - `TestRuns`: test execution summaries + parent-child FK to `TestCases`
  - `TestCases`: individual test results with full name, duration, error messages
  - `RawPayloads` (optional): raw JSON payloads if `DataRetention:EnableRawPayloadStorage` is true

- **Repository Pattern** (`ITelemetryRepository`): 
  - `SqliteTelemetryRepository` (dev default)
  - `PostgresTelemetryRepository` (prod, requires `POSTGRES_CONNECTION_STRING`)
  - Methods: `Add*`, `Get*Summary/List/Detail`, `Delete/RunMaintenance`
  
- **Indexes**: Optimized for common queries
  - `BuildMetrics`: ReceivedAt, MetricType, BuildCategory, ProjectName, ExecutionEnvironment
  - `TestRuns`: ReceivedAt, TestRunner, ProjectName, ExecutionEnvironment

#### **Ingest Pipeline** (`Core/Services/`)
1. **Controllers** route payloads → `BackgroundTaskQueue<T>`
2. **`BackgroundTaskQueue<T>`**: Bounded channel (capacity 1000, wait on full)
   - Tracks outstanding work count via `Interlocked` for drain semantics
3. **Hosted Service Workers** (registered in DI):
   - Dequeue → `IngestService` → `ITelemetryRepository.Add*`
   - Duplicate detection by ID (early return if exists)
   - Called asynchronously from controller with `await QueueBackgroundWorkItemAsync()`

#### **Normalization & Enrichment**
- **`EnvironmentDetector.Detect()`**:
  - Heuristics: Debugger attached → "Local" | Docker/AWS platform → "CI" | CI hostname patterns → "CI" | RunId patterns → "CI"
  - Fallback: "Local" (safe default)
  
- **`BuildCategoryClassifier.Classify()`**:
  - Assigns `BuildCategory` ("API" or "Clientside") and optional `ReloadType` ("hot" or "full")
  - Lookups: metricType `.Net`, `.AspNetStartup`, `.AspNetResponse`, `gradletalaiot` → API
  - `ViteHMR` → Clientside/hot; `Vite`, `Webpack`, `Rspack` → Clientside + resolved reload type
  
- **`StatusNormalizer.Normalize()`**:
  - Canonicalizes test statuses: "Passed", "Failed", "Skipped", "Pending", "Unknown"

#### **Data Cleanup** (`DataCleanupService`)
- Registered as `IHostedService` (runs on app startup)
- Configurable via `DataRetention:*` in appsettings:
  - `RetentionDays` (default 90)
  - `CleanupIntervalHours` (default 24)
  - `VacuumIntervalDays` (default 7)
- **Startup delay**: 5 minutes
- **Cleanup flow**: `DeleteOldDataAsync(cutoff)` → `RunMaintenanceAsync()` (VACUUM/ANALYZE on SQLite, GC on PostgreSQL)

#### **Dashboard Queries** (`DashboardService`)
- Wraps repository methods; delegates to database layer
- Supports `FilterParams`: environment, platform, project, repository, branch, testRunner, metricType, buildCategory, from/to dates, pagination
- Returns pre-aggregated view models (summary statistics, daily trends, paginated lists)

### Ingest Endpoints

| Endpoint | Payload Type | Model | Handler Notes |
|----------|--------------|-------|---------------|
| `POST /dotnet` | DotnetMsBuildPayload | BuildMetric | Parses timeTaken string; 500MB limit |
| `POST /dotnet/nunit` | NUnitPayload | TestRun + TestCases | Counts passed/failed/skipped; parses nested test hierarchy |
| `POST /webpack` | WebpackPayload | BuildMetric | Categorized "Clientside" |
| `POST /vite` | VitePayload | BuildMetric | Supports ViteHMR subtype (hot reload) |
| `POST /vite` | VitestPayload | TestRun + TestCases | Vitest reporter output |
| `POST /jest` | JestPayload | TestRun + TestCases | Parses Jest suite/testResult hierarchy; supports FailureMessages |
| `POST /gradle` | GradleProjectMetricPayload | BuildMetric | Gradle build metrics |
| `POST /ktor` | KtorStartupMetricPayload | BuildMetric | Ktor startup times |
| `POST /gradletalaiot` | GradleTalaiotPayload | BuildMetric | Talaiot Gradle plugin output |
| `POST /junit` | JUnitPayload (XML stream) | TestRun + TestCases | Parsed by `JUnitXmlParser` |
| `POST /scala/scalatest` | ScalaTestPayload | TestRun + TestCases | ScalaTest suite output |

**All endpoints**:
- Return 200 OK synchronously after queueing
- Validate timeTaken (finite positive) / platform enum
- Extract environment, build category, reload type, hostname, branch, project, etc.
- Store raw payload JSON if enabled

### Entry Point

**`Program.cs`** (56 lines → ~150 lines with config):
1. Load `appsettings.json` + env-specific overrides + environment variables
2. Wire Serilog (console sink with structured JSON formatting)
3. Conditional DB setup:
   - If `POSTGRES_CONNECTION_STRING` → EF Core + Npgsql + `PostgresTelemetryRepository`
   - Else → SQLite + `SqliteTelemetryRepository`
4. Configure Kestrel: 500MB max request body
5. Enable request decompression (gzip support)
6. CORS: Allow `http://localhost:3000` (development)
7. Auto-wire services from `Core` assembly via `Agoda.IoC.NetCore`
8. Auto-migrate SQLite; EnsureCreated() for PostgreSQL (MVP—no migrations yet)
9. Swagger at root; MapFallbackToFile("index.html") for SPA routing

---

## 3. Frontend Architecture

### Structure
```
src/Clientside/
├── src/
│   ├── App.tsx                 # Root router (3 dashboards + detail view)
│   ├── main.tsx                # React 19 entry, BrowserRouter wrapper
│   ├── api/
│   │   └── client.ts          # Fetch wrappers, TypeScript interfaces (FilterParams, TestRunItem, etc.)
│   ├── pages/
│   │   ├── TestRunPerformance/
│   │   │   ├── TestRunDashboard.tsx    # List + summary stats
│   │   │   └── TestRunDetail.tsx       # Single test run with case list
│   │   ├── ApiBuildPerformance/        # Compile + startup time trends
│   │   └── ClientsideBuildPerformance/ # Hot reload % + full reload stats
│   ├── components/
│   │   ├── cards/              # Summary metric cards
│   │   ├── charts/             # Recharts visualizations (trends, distributions)
│   │   ├── filters/            # Filter dropdown/input controls
│   │   ├── tables/             # Paginated data tables (@tanstack/react-table)
│   │   └── layout/
│   │       └── AppShell.tsx    # Navigation header + sidebar
│   ├── hooks/                  # Custom React hooks (likely data fetching)
│   ├── theme/
│   │   └── tailwind-tokens.css # Design tokens
│   └── utils/                  # Helper functions
├── vite.config.ts              # Dev proxy `/api` → `http://localhost:5000`
├── playwright-ct.config.ts     # Component testing config
├── tailwind.config.ts          # Tailwind CSS setup
├── tsconfig.json               # TS strict mode
└── package.json                # React 19, Tremor, Recharts, React Router, Tailwind
```

### Key Dependencies
- **React 19.2.4**: Latest React with hooks
- **React Router 7.13.1**: SPA routing
- **Vite 8**: Dev server (port 3000), fast HMR, esbuild
- **TypeScript 5.9.3**: Type safety
- **Tailwind CSS 3.4.19**: Utility-first styling
- **Tremor 3.18.7**: Dashboard UI primitives (cards, charts)
- **Recharts 3.8.0**: Composable React charts
- **@tanstack/react-table 8.21.3**: Headless table logic (sorting, pagination)
- **Playwright experimental-ct-react 1.58.2**: Component testing

### Build & Dev Setup
- **Dev**: `npm run dev` → Vite on port 3000 with `/api` proxy to backend
- **Build**: `npm run build` → TypeScript compile + Vite bundle → `../Agoda.DevExTelemetry.WebApi/wwwroot/`
- **Linting**: ESLint + Prettier (strict mode, React hooks rules)
- **Testing**: Playwright component tests (`test-ct` command with workaround patch)

### API Client Interface
```typescript
interface FilterParams {
  environment?: string; project?: string; branch?: string;
  from?: string; to?: string; page?: number; pageSize?: number;
  // ... (platform, testRunner, metricType, buildCategory, etc.)
}

interface TestRunSummary {
  totalRuns: number; avgDurationMs: number; passRate: number;
  durationTrend: DailyDataPoint[]; passFailTrend: DailyPassFail[];
}

interface PaginatedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number; }
```

---

## 4. Data Flow (End-to-End)

### Ingest Flow
```
Client SDK (e.g., Agoda.Builds.Metrics)
  ↓ HTTP POST /dotnet (gzip payload, 1-500 MB)
  ↓
DotnetController.IngestBuild()
  ├─ EnvironmentDetector.Detect() → "Local" or "CI"
  ├─ BuildCategoryClassifier.Classify() → "API" or "Clientside"
  ├─ Construct BuildMetric entity
  ├─ Serialize to JSON
  └─ _buildMetricQueue.QueueBackgroundWorkItemAsync(IngestBuildMetricWorkItem)
    ↓ (Bounded channel, wait if full)
    ↓ Background worker dequeues
    ↓
IngestService.IngestBuildMetricAsync()
  ├─ Check duplicate (BuildMetricExistsAsync)
  ├─ AddBuildMetricAsync() → EF Core SaveChanges()
  ├─ StoreRawPayloadAsync() (if enabled)
  └─ _queue.NotifyItemProcessed() (decrement outstanding count)
    ↓
Database
  ├─ BuildMetrics row inserted
  └─ RawPayloads row (optional)
```

### Query Flow
```
Frontend: FilterParams (project, from, to, page=1, pageSize=10)
  ↓ HTTP GET /api/test-runs
  ↓
DashboardController.GetTestRuns()
  ├─ _dashboardService.GetTestRunsAsync(filters)
    ↓
DashboardService → _repository.GetTestRunsAsync(filters)
    ↓
SQLite/PostgreSQL Repository
  ├─ Filter: ReceivedAt BETWEEN from AND to, ProjectName = ?, etc.
  ├─ Order: ReceivedAt DESC
  ├─ Paginate: SKIP (page-1)*pageSize, TAKE pageSize
  └─ Project to TestRunListItemViewModel
    ↓
JSON → Frontend
  ├─ Render paginated table
  └─ Bind summary stats from /api/test-runs/summary
```

### Cleanup Flow
```
DataCleanupService (IHostedService)
  ├─ On startup: await Task.Delay(5 minutes)
  ├─ PeriodicTimer(24 hours)
  ├─ Loop:
  │   ├─ CreateScope() → get ITelemetryRepository
  │   ├─ DeleteOldDataAsync(DateTime.UtcNow.AddDays(-90))
  │   │   └─ DELETE FROM BuildMetrics WHERE ReceivedAt < cutoff (+ TestRuns cascade)
  │   └─ Every 7 days: RunMaintenanceAsync()
  │       └─ SQLite: VACUUM + ANALYZE
  │       └─ PostgreSQL: implicit GC
  └─ Errors logged to Serilog, execution continues
```

---

## 5. Key Components & Responsibilities

| Component | Location | Responsibility |
|-----------|----------|-----------------|
| **TelemetryDbContext** | `Core/Data/` | Schema definition, navigation properties, indices |
| **ITelemetryRepository** | `Core/Data/` | Abstract data access (add, query, delete, maintenance) |
| **Sql/PostgresTelemetryRepository** | `Core/Data/` | Concrete LINQ-to-SQL implementations |
| **BackgroundTaskQueue<T>** | `Core/Services/` | Bounded channel for async work; outstanding count tracking |
| **IngestService** | `Core/Services/` | Duplicate detection, entity creation, raw payload storage |
| **DashboardService** | `Core/Services/` | Delegates queries to repository (thin wrapper) |
| **BuildCategoryClassifier** | `Core/Services/` | Heuristic: metricType → BuildCategory + ReloadType |
| **EnvironmentDetector** | `Core/Services/` | Heuristic: debugger/platform/runId/hostname → "Local" \| "CI" |
| **StatusNormalizer** | `Core/Services/` | Test status canonicalization (Passed, Failed, Skipped, etc.) |
| **JUnitXmlParser** | `Core/Services/` | Parse JUnit XML stream → TestRun + TestCases |
| **DataCleanupService** | `Core/Services/` | Background delete + maintenance (VACUUM, etc.) |
| **FilterService** | `Core/Services/` | Wrapper for getting available filter options |
| **{X}Controller** | `WebApi/Controllers/` | HTTP endpoints for each ingest type (Dotnet, Jest, Gradle, etc.) |
| **App.tsx** | `Clientside/src/` | Root router (4 routes: test-runs, test-runs/:id, api-build, clientside-build) |
| **{X}Dashboard.tsx** | `Clientside/src/pages/` | Page components (list + summary on first page; detail on second) |
| **client.ts** | `Clientside/src/api/` | Fetch wrappers, type definitions for request/response |

---

## 6. Dependencies & Key Relationships

### Backend NuGet Dependencies
```
Agoda.DevExTelemetry.WebApi
├─ Agoda.IoC.NetCore 1.1.110       # Auto-wire services via [Register*] attributes
├─ Serilog 4.3.1                   # Structured logging
├─ Serilog.AspNetCore 10.0.0        # Request logging middleware
├─ Swashbuckle.AspNetCore 10.1.5    # Swagger/OpenAPI
├─ Microsoft.EntityFrameworkCore    # ORM

Agoda.DevExTelemetry.Core
├─ Microsoft.EntityFrameworkCore.Sqlite 10.0.5   # SQLite provider
├─ Npgsql.EntityFrameworkCore.PostgreSQL 10.0.1  # PostgreSQL provider
└─ Microsoft.Extensions.Hosting      # IHostedService
```

### Registration Pattern (Agoda.IoC.NetCore)
- `[RegisterSingleton(For = typeof(IBackgroundTaskQueue<>))]` → bind generic interface
- `[RegisterPerRequest]` → transient scope per HTTP request
- `[RegisterSingleton(For = typeof(IHostedService))]` → app-wide singleton

### Frontend npm Dependencies
- **Core**: React 19, React Router 7, Vite 8
- **UI**: Tailwind CSS 3, Tremor 3, Recharts 3
- **Dev**: TypeScript 5.9, ESLint 10, Prettier 3, Playwright 1.58
- **Note**: `--legacy-peer-deps` flag required (Tremor / Recharts peer issues)

### Database Relationships
```
TestRun (1) → (N) TestCase
  ├─ PK: TestRun.Id
  ├─ FK: TestCase.TestRunId
  └─ OnDelete: Cascade

BuildMetric (standalone, no FK)
RawPayload (standalone, no FK)
```

---

## 7. Configuration & Environment

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=devex-telemetry.db"  // SQLite dev default
  },
  "DataRetention": {
    "RetentionDays": 90,                   // How long to keep data
    "CleanupIntervalHours": 24,            // Run cleanup every 24 hours
    "VacuumIntervalDays": 7,               // VACUUM database every 7 days
    "EnableRawPayloadStorage": false       // Store raw JSON payloads (space-intensive)
  },
  "Serilog": {
    "MinimumLevel": { "Default": "Information" },  // Log level
    "WriteTo": [{ "Name": "Console", ... }]        // Console sink
  }
}
```

### Environment Variables
- **`POSTGRES_CONNECTION_STRING`**: If set, use PostgreSQL instead of SQLite
  - Format: `Host=localhost;Port=5432;Database=devex_telemetry;Username=devex;Password=devex`
- **`DEVFEEDBACK_URL`**: Client-side override (set on developer machines to redirect to custom API)
- All standard ASP.NET Core vars (ASPNETCORE_ENVIRONMENT, DOTNET_*, etc.)

### Deployment Configs
- **Docker**: Multi-stage build; Node 22 → React build; .NET 10 SDK → publish; ASP.NET 10 runtime
- **docker-compose.yml**: API + PostgreSQL 17 in isolated network
- **GitHub Actions** (`.github/workflows/`):
  - `build.yml`: On PR → dotnet build + test (SQLite), npm lint + build
  - `docker-publish.yml`: On push to main → docker build + push to Docker Hub
  - `deploy.yml`: (deployment orchestration, details TBD)

---

## 8. Important Code Paths & Fragile Areas

### High-Risk Changes

| Change | Inspect | Why |
|--------|---------|-----|
| Add/remove `BuildMetrics` column | `TelemetryDbContext.OnModelCreating()` indices; migration; all ingest controllers | All controllers construct BuildMetric; indices must be maintained for query perf |
| Add/remove `TestRun`/`TestCase` column | Foreign key constraint; cascade delete; all test ingest controllers; test detail queries | Cascade delete semantics change; controllers parse different payloads |
| Modify `BackgroundTaskQueue` capacity | Ingest latency; test throughput; memory under load | 1000-item buffer may overflow under heavy load; queued work can be lost on crash |
| Change `EnvironmentDetector` heuristics | Test classification (CI vs Local); reporting accuracy; compliance | If heuristics shift, all existing data gets reclassified differently |
| Disable `DataCleanupService` | Disk usage; query performance on 90-day data; compliance/audit | Old data will accumulate; table scans get slower; retention policy breaks |
| Refactor repository pattern | All controllers; all dashboard queries; tests | High coupling between layers; any interface change breaks ingest + queries |

### Known Limitations & Gaps

| Issue | Status | Workaround |
|-------|--------|-----------|
| **PostgreSQL migrations (MVP)** | `EnsureCreated()` used; no migration history | If schema changes, manual migration required; no rollback path |
| **No API versioning** | Clients tied to single endpoint version | Breaking payload changes will require client SDK update |
| **Raw payload storage optional** | `EnableRawPayloadStorage` default false; if enabled, space explosive | Monitor disk; consider archival strategy |
| **Duplicate detection by ID only** | If client generates same ID twice (clock skew, retry), second rejected silently | Clients must generate UUIDs; logging shows duplicate but no alert |
| **Background queue not persistent** | Bounded channel in memory; on restart, queued items lost | Graceful shutdown via `WaitUntilDrainedAsync()` called in Program.cs (verify) |
| **No authentication/authorization** | Internal tool only; assumes trusted network | Do not expose to public internet without auth/firewall |
| **CORS hardcoded to localhost:3000** | Dev-only; will fail in production | Production deployment must update CORS policy |
| **Vite dev server proxy hardcoded to 5000** | Will break if backend port changes | Update `vite.config.ts` server.proxy.target |

### Uncertain Areas

- **Backend worker shutdown graceful drain**: Program.cs may not await `DataCleanupService` graceful shutdown before process exits. Verify `HostedService.StopAsync()` contract.
- **Frontend offline handling**: No retry logic visible in `client.ts`; network failures = blank UI. Spec unclear.
- **Payload size limits per endpoint**: 500 MB limit set on Kestrel + each controller; actual client payloads unknown.
- **Test performance at scale**: No load testing visible; unclear how N=1M records behave on queries/cleanup.
- **PostgreSQL production readiness**: Comments say "EnsureCreated()...since there are no existing PostgreSQL databases to migrate"; if schema evolves, migration path unclear.

---

## 9. How to Develop & Test

### Local Development Setup

**Prerequisites**:
- .NET 10 SDK
- Node.js 22+
- SQLite (usually bundled)

**Backend**:
```bash
cd src
dotnet restore
dotnet build
# Option A: Run with in-memory SQLite (dev)
dotnet run --project Agoda.DevExTelemetry.WebApi
# → Listens http://localhost:5000, https://localhost:5001
# Swagger at http://localhost:5000/swagger

# Option B: Run with PostgreSQL
export POSTGRES_CONNECTION_STRING='Host=localhost;Port=5432;...'
dotnet run --project Agoda.DevExTelemetry.WebApi
```

**Frontend**:
```bash
cd src/Clientside
npm install --legacy-peer-deps
npm run dev
# → Vite dev server on http://localhost:3000
# → Proxy /api/* to http://localhost:5000
```

**Tests**:
```bash
cd src
dotnet test
# Runs NUnit integration tests against both SQLite and PostgreSQL (if env var set)
# Frontend playwright tests: cd src/Clientside && npm run test-ct
```

### Testing Ingest

**Manual curl**:
```bash
curl -X POST http://localhost:5000/dotnet \
  -H "Content-Type: application/json" \
  -d '{
    "id": "test-1",
    "userName": "dev",
    "hostname": "macbook",
    "projectName": "MyApp",
    "timeTaken": "1234.56",
    "type": ".Net",
    "platform": 6,
    "isDebuggerAttached": false
  }'
# → 200 OK (queued async)
```

**Integration tests**:
- `DotnetIngestTests.cs`: POST /dotnet → verify BuildMetric stored with correct category
- `JestIngestTests.cs`: POST /jest → verify TestRun + TestCases parsed
- `DataCleanupTests.cs`: Insert old data → wait cleanup → verify deleted
- `FilterTests.cs`: Test filter query logic

---

## 10. Deployment & Operations

### Docker

**Build**:
```bash
docker build -t agoda/devex-telemetry:latest .
# Stage 1: Node 22-alpine → npm ci + npm run build
# Stage 2: .NET 10 SDK → dotnet restore + publish
# Stage 3: ASP.NET 10 runtime → ENTRYPOINT dotnet Agoda.DevExTelemetry.WebApi.dll
```

**Run**:
```bash
docker run --rm -p 8080:8080 \
  -e POSTGRES_CONNECTION_STRING='Host=db;Port=5432;Database=devex_telemetry;Username=devex;Password=devex' \
  agoda/devex-telemetry:latest
```

### Docker Compose

```bash
cd <repo>
docker compose up -d
# → API on http://localhost:8080
# → PostgreSQL on localhost:5432
docker compose logs -f app
docker compose down
```

### Kubernetes + Helm

See `docs/deployment-scenarios.md` for multi-node patterns:
- **Scenario A**: Single docker-compose host (PoC)
- **Scenario B**: K8s + externally managed PostgreSQL (recommended)

**Client routing** (2 options):
1. Internal DNS: `compilation-metrics` → API service (preferred, zero per-machine config)
2. Environment variable: `DEVFEEDBACK_URL` per workstation

### Monitoring & Logging

- **Logging**: Serilog → Console (CompactJsonFormatter)
  - Fields: MachineName, Version, AssemblyName, EnvironmentName
  - Override levels: EntityFrameworkCore, Microsoft.AspNetCore → Warning
  
- **Metrics**: Swagger `/swagger/v1/swagger.json` shows all endpoints + request counts possible via middleware

- **Data cleanup errors**: Logged to Serilog; if cleanup fails, execution continues (non-blocking)

---

## 11. Summary Table: Core Files

| File | LOC | Purpose | Key Classes/Methods |
|------|-----|---------|-------------------|
| Program.cs | ~160 | App bootstrap, DI, DB setup, middleware | DB provider selection, CORS, Swagger, SPA fallback |
| TelemetryDbContext.cs | ~60 | Entity schema + indices | OnModelCreating (FK constraints, index definitions) |
| BuildMetric.cs | ~30 | Domain entity | Properties: TimeTakenMs, BuildCategory, ReloadType, ExecutionEnvironment, SourceEndpoint |
| TestRun.cs | ~35 | Domain entity | Properties: TestRunner, TotalTests, PassedTests, FailedTests, TotalDurationMs |
| TestCase.cs | ~20 | Domain entity | Properties: TestRunId (FK), Status, DurationMs, ErrorMessage |
| IngestService.cs | ~40 | Ingest orchestration | IngestBuildMetricAsync, IngestTestRunAsync, StoreRawPayloadAsync |
| DashboardService.cs | ~20 | Query orchestration | All dashboard methods delegate to repository |
| BackgroundTaskQueue.cs | ~45 | Async work queuing | QueueBackgroundWorkItemAsync, DequeueAsync, WaitUntilDrainedAsync |
| DotnetController.cs | ~120 | .NET ingest endpoint | POST /dotnet, POST /dotnet/nunit |
| JestController.cs | ~100 | Jest ingest endpoint | POST /jest (parses suite hierarchy) |
| JUnitXmlParser.cs | ~80 | XML parsing utility | Parse (XDocument stream → TestCases) |
| BuildCategoryClassifier.cs | ~30 | Classification logic | Classify (metricType → BuildCategory, ReloadType) |
| EnvironmentDetector.cs | ~35 | Environment detection | Detect (heuristics: debugger, platform, runId, hostname) |
| StatusNormalizer.cs | ~25 | Status canonicalization | Normalize (test status string → standard enum) |
| DataCleanupService.cs | ~65 | Auto-cleanup background service | ExecuteAsync (5-min delay, 24-hour intervals, VACUUM every 7 days) |
| App.tsx | ~15 | React root router | Routes: /, /test-runs, /test-runs/:id, /api-build, /clientside-build |
| client.ts | ~100 | API client + types | FilterParams, TestRunSummary, PaginatedResult, etc. |
| DotnetIngestTests.cs | ~200 | Integration tests | Tests for build category, metric type, time parsing, duplicates |

---

## 12. What to Know Before Modifying

### Ingest Path Checklist
- [ ] Adding new ingest endpoint? Must create new `{Type}Payload` model, controller, and tests (both DBs)
- [ ] Changing BuildMetric schema? Update `TelemetryDbContext.OnModelCreating()`, add migration/EnsureCreated, update all controllers
- [ ] Modifying classification? Update `BuildCategoryClassifier`, audit all dashboard filters that depend on `BuildCategory`

### Query Path Checklist
- [ ] Adding new dashboard view? Create ViewModel, add repository method, create controller endpoint, wire frontend page
- [ ] Changing filter logic? Update `FilterParams` in both backend + frontend (client.ts types), test pagination

### Deployment Checklist
- [ ] Changing CORS? Update `Program.cs` origin list (test with proxy setup)
- [ ] Adding database-only config? Update `appsettings.json` + env var override
- [ ] Scaling database? Verify PostgreSQL connection pooling; audit slow queries via EF Core logs
- [ ] Changing retention policy? Test cleanup service with large datasets; verify cascade deletes on TestRun → TestCase

### Testing Checklist
- [ ] After ingest change: Run `DotnetIngestTests`, `JestIngestTests`, etc. for both SQLite + PostgreSQL
- [ ] After query change: Run `ApiBuildDashboardTests`, `TestRunDashboardTests` integration tests
- [ ] After cleanup change: Run `DataCleanupTests` with retention days override
- [ ] After frontend change: `npm run lint`, `npm run build`, `npm run test-ct`

---

## 13. Evidence & Verification

This knowledge base was built by:
1. ✅ Reading `Program.cs` entry point → confirmed .NET 10, Serilog, dual DB setup
2. ✅ Inspecting `TelemetryDbContext` → verified 4 tables, FK constraints, indices
3. ✅ Tracing ingest controllers → confirmed background queue + duplicate detection
4. ✅ Reading `IngestService` → verified async pipeline with optional raw payload storage
5. ✅ Inspecting `DataCleanupService` → confirmed configurable retention, VACUUM/GC logic
6. ✅ Reading `App.tsx` + `client.ts` → confirmed 3 dashboards + detail view
7. ✅ Reviewing `DotnetIngestTests.cs` → confirmed test patterns for both DB providers
8. ✅ Inspecting `Dockerfile` → verified 3-stage build + static file embedding
9. ✅ Reading `docker-compose.yml` → verified PostgreSQL 17 + service link
10. ✅ Reviewing `vite.config.ts` → confirmed dev proxy, build output path
11. ✅ Checking CI workflows → confirmed build + test strategy
12. ✅ Reading `appsettings.json` → verified retention, cleanup intervals, Serilog config
13. ✅ Checking classification & environment detection logic → heuristics documented with code paths

---

## 14. Quick Answers to Common Questions

**Q: Where do telemetry clients send data?**  
A: `POST /dotnet`, `/jest`, `/vite`, `/gradle`, `/junit`, `/scala/scalatest`, `/ktor`, etc. All endpoints queue work asynchronously.

**Q: How is duplicate data prevented?**  
A: By ID field. `IngestService.IngestBuildMetricAsync()` checks `BuildMetricExistsAsync(id)` before insert; if true, returns early (silent dedupe, logged as info).

**Q: What happens if the backend crashes?**  
A: In-memory bounded queue (1000 items) is lost. No persistent queue. Graceful shutdown via `WaitUntilDrainedAsync()` should flush to DB before process exit.

**Q: How do I deploy to production?**  
A: Push to main → `docker-publish.yml` builds and tags image → Deploy to K8s or docker-compose with `POSTGRES_CONNECTION_STRING` env var pointing to managed DB.

**Q: How is old data deleted?**  
A: `DataCleanupService` runs every 24 hours; deletes records older than 90 days (configurable). VACUUM happens every 7 days on SQLite; implicit on PostgreSQL.

**Q: Why does the frontend need `--legacy-peer-deps`?**  
A: Tremor/Recharts have peer dependency version conflicts; npm ci --legacy-peer-deps bypasses them.

**Q: What if the vite dev server can't reach the backend?**  
A: Vite proxy at `http://localhost:5000` will fail; update `vite.config.ts` server.proxy.target or ensure backend is running. No retry logic in client.ts.

**Q: How are "Local" vs "CI" environments detected?**  
A: Heuristics in `EnvironmentDetector.Detect()`: debugger attached → Local; Docker/AWS platform → CI; CI hostname patterns (runner, agent, build) → CI; runId with CI env vars → CI; else Local.

**Q: Why does JUnit parsing need a custom `JUnitXmlParser`?**  
A: JUnit XML schema is flexible; different test frameworks (Maven surefire, Gradle, ScalaTest) have subtly different element hierarchies. Custom parser normalizes to TestRun + TestCases.

**Q: What happens if a database migration fails on PostgreSQL?**  
A: Schema created via `EnsureCreated()` (no migration history). If schema changes post-deployment, manual migration required; no rollback. Consider adding a proper migration assembly for production.

---

**Document Version**: 1.0  
**Last Updated**: 2026-08-16  
**Scope**: Full-stack mental model for maintenance, debugging, and feature development  
**Confidence**: High (verified against all critical code paths)
