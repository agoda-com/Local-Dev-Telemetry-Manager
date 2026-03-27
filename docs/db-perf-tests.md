# DB Performance Tests (Manual)

This project adds a dedicated performance harness for dashboard read endpoints.

Project:
- `src/Agoda.DevExTelemetry.DbPerfTests/Agoda.DevExTelemetry.DbPerfTests.csproj`

Workflow:
- `.github/workflows/db-perf-manual.yml`
- Triggered manually via **workflow_dispatch**
- Supports `sqlite`, `postgres`, or `both`

## What it does

1. Boots the API with test host (`WebApplicationFactory<Program>`)
2. Verifies app startup (`/api/health`)
3. Seeds deterministic randomized data with configurable cardinality
4. Runs warm-up calls
5. Measures endpoint timings for selected dashboard endpoints
6. Produces JSON artifact per DB engine with mean/p50/p95/p99

## Local run examples

### SQLite

```bash
PERF_DB_PROVIDER=sqlite \
  dotnet test src/Agoda.DevExTelemetry.DbPerfTests/Agoda.DevExTelemetry.DbPerfTests.csproj
```

### PostgreSQL (external/local)

```bash
PERF_DB_PROVIDER=postgres \
PERF_POSTGRES_CONNECTION='Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres' \
  dotnet test src/Agoda.DevExTelemetry.DbPerfTests/Agoda.DevExTelemetry.DbPerfTests.csproj
```

## Optional seed overrides

- `PERF_BUILD_METRICS` (default: 20000)
- `PERF_TEST_RUNS` (default: 10000)
- `PERF_TEST_CASES_PER_RUN` (default: 8)
- `PERF_RAW_PAYLOADS` (default: 10000)

## Output

JSON timing files are written under test work directory:
- `db-perf-results/endpoint-timings-sqlite.json`
- `db-perf-results/endpoint-timings-postgresql.json`

CI uploads these JSON files as workflow artifacts.

## Notes

- This is currently manual-only (not nightly, not PR-blocking).
- Relative gating/delta comparison can be added after stabilization.
