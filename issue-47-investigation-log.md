# Issue #47 — Investigation Log

## 1. System boundary

- `devfeedback-js` is the client telemetry library.
- `Local-Dev-Telemetry-Manager` is the server ingestion, persistence, and dashboard application.
- Backend stack: ASP.NET Core / .NET 10, EF Core, SQLite/PostgreSQL, React dashboard.
- Relevant development telemetry endpoints: `/webpack`, `/vite`, `/rspack`, with #47 adding `/command`.

## 2. Existing ingestion and persistence

Current build telemetry flow:

```text
HTTP request
  ↓
Controller
  ↓
Payload DTO
  ↓
IngestService
  ↓
IngestBuildMetricWorkItem
  ↓
BuildMetric
  ↓
TelemetryDbContext
  ↓
BuildMetrics
```

The work item also receives raw payload data:

```text
IngestBuildMetricWorkItem
├── BuildMetric
├── RawPayloadJson
├── RawPayloadEndpoint
└── RawPayloadContentType
```

### `BuildMetric`

- Entity is independent: no inheritance or foreign keys.
- Table: `BuildMetrics`.
- `Id`: `string`, primary key.
- Existing indexes: `BuildCategory`, `ExecutionEnvironment`, `MetricType`, `ProjectName`, `ReceivedAt`.
- `TimeTakenMs`: `REAL`.
- Existing build-specific fields include `MetricType`, `BuildCategory`, `ReloadType`, `TimeTakenMs`, `ToolVersion`, `ExtraData`.
- `SourceEndpoint` is ingestion metadata rather than build-specific data.

### `RawPayload`

`RawPayloads` stores:

```text
Id
ContentType
Endpoint
PayloadJson
ReceivedAt
```

`PayloadJson` is `TEXT`.

Conclusion: raw JSON preserves information that is not normalized, but structured fields are required for efficient filtering, grouping, indexing, and aggregation.

## 3. Existing BuildMetric read path

```text
GET /api/build-metrics
  ↓
DashboardController
  ↓
GetBuildMetricsAsync(filters)
  ↓
Db.BuildMetrics
  ↓
ApplyBuildMetricFilters(...)
  ↓
BuildMetricListItemViewModel
```

The view model currently exposes:

```text
Id
ReceivedAt
ProjectName
MetricType
BuildCategory
ReloadType
TimeTakenMs
ToolVersion
ExecutionEnvironment
```

`ApplyBuildMetricFilters` uses:

```text
environment
platform
project
repository
branch
from
to
```

The existing dashboard is therefore tightly coupled to build-oriented concepts.

## 4. Why command events should not be forced into `BuildMetric`

Command events contain:

```text
sessionId
command
phase
exitCode
success
signal
timeTaken
```

plus phase-specific data.

Existing `BuildMetric` concepts such as:

```text
MetricType
BuildCategory
ReloadType
TimeTakenMs
```

do not have equivalent semantics for events such as:

```text
install
devserver
clientready
```

`timeTaken` is also semantically different from existing `timeTakenMs`.

Current conclusion: **do not blindly persist command events as `BuildMetric` rows.** Final persistence model is still open.

## 5. Command payload model

Observed command phases:

```text
install
devserver
devserverAborted
clientready
installWithNpmTimers
```

All use:

```text
type = command
```

`installWithNpmTimers` is still an `install` phase with additional timer data; it does not require a separate event type.

Common command fields observed:

```text
id
sessionId
userName
cpuCount
hostname
platform
os
timeTaken
branch
projectName
repository
repositoryName
timestamp
builtAt
totalMemory
cpuModels
cpuSpeed
nodeVersion
v8Version
commitSha
customIdentifier
type
phase
command
exitCode
success
```

## 6. Command field classification

| Field | Shape | Dedicated column today? | Meaning | Category |
|---|---|---:|---|---|
| `sessionId` | string | No | Development-session identifier | Shared development-event metadata |
| `command` | string | No | Command/process being measured | Command-specific |
| `phase` | string | No | Lifecycle phase | Command-specific |
| `exitCode` | integer | No | Process exit status | Command outcome |
| `success` | boolean | No | Whether command succeeded | Command outcome |
| `signal` | string | No | Termination signal | Command outcome |
| `errorCount` | integer | No | Reserved; not emitted today | Command outcome |
| `timeTaken` | number | No | Command-event duration | Command-specific |
| `packageManager` | string | No | `npm`, `yarn`, `pnpm` | Install-specific |
| `packageManagerVersion` | string | No | Package manager version | Install-specific |
| `coldInstall` | boolean | No | Whether `node_modules` was absent before install | Install-specific |
| `lockfileChanged` | boolean | No | Whether lockfile changed | Install-specific |
| `measurementSource` | string | No | How install duration was measured | Install-specific |
| `npmTimers` | map/object | No | npm phase/package timings | Install-specific dynamic data |
| `prebundled` | tri-state boolean | No | Vite dependency prebundling state | Devserver-specific |
| `domContentLoadedMs` | number | No | DOM content-loaded timing | Clientready-specific |
| `firstContentfulPaintMs` | number | No | FCP timing | Clientready-specific |
| `spooledAt` | integer | No | When event entered local spool | Delivery metadata |

Semantic checks:

- `sessionId` correlates events within one development session.
- `phase` identifies the lifecycle phase, not the session.
- `exitCode`, `success`, and `signal` represent different aspects of command outcome.
- `measurementSource` must not be mixed across different timing semantics.
- `prebundled` is tri-state; absent/unknown must not become `false`.
- `timestamp` and `spooledAt` represent different clocks.
- `npmTimers` has dynamic keys and a client-side maximum of 500 entries.

## 7. `npmTimers` storage investigation

Issue #47 explicitly rejects storing only a known subset of timers.

Options considered:

1. JSON column on the install event.
2. Child table such as `install_timer(event_id, timer_name, duration_ms)`.
3. Known subset — rejected.

Requirements established during investigation:

- Timer-level analytics are required.
- Timers must be queryable alongside event dimensions such as session and time range.
- Expected scale discussed: ~10,000 events currently, with capability to reach ~1 million events.
- 500 timers/event is a maximum, not an expected average.
- Original `npmTimers` JSON should remain available as the source representation.
- Existing `RawPayloads.PayloadJson` already preserves the original payload.
- A normalized child-table representation is currently the leading option because the issue's dashboard use case requires timer-level analytics.

Current direction:

```text
RawPayloads
  └── original npmTimers JSON

Install/Command event
  └── normalized timer rows for analytics
```

No final schema has been implemented or approved yet.

## 8. Existing entity comparison

`BuildMetric` and `TestRun` share substantial telemetry context:

```text
Id
ReceivedAt
UserName
CpuCount
Hostname
Platform
Os
Branch
ProjectName
Repository
RepositoryName
IsDebuggerAttached
ExecutionEnvironment
SourceEndpoint
ExtraData
```

Their domain-specific data differs substantially:

```text
BuildMetric:
  TimeTakenMs
  MetricType
  BuildCategory
  ReloadType
  ToolVersion
  CommitSha

TestRun:
  RunId
  TestRunner
  TotalTests
  PassedTests
  FailedTests
  SkippedTests
  TotalDurationMs
  TestCases
```

`ExtraData` was verified as build-specific structured data containing fields such as:

```text
date
ide
buildKind
requestedTasks
taskCount
executedTaskCount
upToDateTaskCount
fromCacheTaskCount
failedTaskCount
compileTaskCount
compileTimeMs
taskTimeMs
projects
```

Therefore `ExtraData` should not be treated as generic telemetry metadata.

## 9. Client-side contract discovery

`devfeedback-js` already defines a shared client abstraction:

```text
CommonMetadata
  ├── WebpackBuildData
  ├── ViteBuildData
  ├── RspackBuildData
  └── CommandBuildData
```

`CommandBuildData` is already a first-class client event type.

`CommonMetadata` contains shared context such as:

```text
id
sessionId
userName
git information
CPU/memory
timestamp
commitSha
...
```

This is strong evidence that the client contract already separates:

```text
common telemetry context
+
event-specific payload
```

The backend currently does not have an equivalent shared event abstraction.

## 10. `sessionId` requirement

Issue #47 explicitly requires `sessionId` on:

```text
webpack
vite
vitehmr
rspack
rsbuild
command
```

Purpose:

```text
install
  ↓
devserver
  ↓
first vitehmr
```

must be correlated into one development-session timeline.

Requirements validated from the issue:

- `sessionId` is a UUID string.
- Existing clients may omit it.
- Server must accept and store `null` for old clients.
- New clients sending `sessionId` to old endpoints must not be rejected as an unknown field.
- `sessionId` must be indexed because important queries group by it.

`TestRun` is not included in this explicit scope.

## 11. Current architectural position

Validated:

- Command telemetry is a distinct domain from the existing `BuildMetric` model.
- `sessionId` is the primary correlation key across the development-event family.
- Client-side code already models common metadata separately from event-specific data.
- Raw payloads are retained independently.
- Timer-level analytics justify normalized timer data; option 3 is rejected.

Still open:

- Exact backend representation of the shared development-event metadata.
- Separate `CommandEvent` entity vs shared abstraction.
- Physical database layout for a unified session/event stream.
- Exact `npmTimers` child-table schema and indexes.
- Unknown JSON-field tolerance in current server validation.
- `/rspack` / `rsbuild` ingestion path.
- Retention/cardinality constraints.
- Exact command-event dashboard/read requirements.

Current phase:

```text
Issue requirements
  ↓
Client contract
  ↓
Existing backend model
  ↓
Read/query model
  ↓
Gap analysis
  ↓
Domain boundary
  ↓
Persistence design
```
