| Field | Shape | Dedicated column today? | Meaning | Risk if only raw JSON |
| --- | --- | --- | --- | --- |
| `sessionId` | Scalar / string | No | Development-session identifier used to correlate events | Can't efficiently correlate events into one development session |
| `command` | Scalar / string | No | Command/process being measured, e.g. `npm install`, `vite dev` | Can't efficiently filter or aggregate by command |
| `phase` | Scalar / string | No | Lifecycle phase of the command event | Can't efficiently group/filter by lifecycle phase |
| `exitCode` | Scalar / integer | No | Process exit status | Hard to analyze failures and abnormal exits |
| `success` | Scalar / boolean | No | Whether the command completed successfully | Can't efficiently query success/failure |
| `signal` | Scalar / string | No | Signal that terminated the command, e.g. `SIGINT` | Hard to analyze abandonment/interruption |
| `errorCount` | Scalar / integer | No | Reserved error count; not emitted today | Low current priority because client doesn't emit it yet |
| `timeTaken` | Scalar / number | **No** | Command-event duration in milliseconds | Existing `timeTakenMs` has different semantics, so treating them as the same field could corrupt analysis |
| `packageManager` | Scalar / string | No | Package manager used: `npm`, `yarn`, or `pnpm` | Can't efficiently compare installs by package manager |
| `packageManagerVersion` | Scalar / string | No | Version of the package manager | Can't efficiently compare performance across versions |
| `coldInstall` | Scalar / boolean | No | Whether `node_modules` was absent before installation | Can't efficiently compare cold vs. warm installs |
| `lockfileChanged` | Scalar / boolean | No | Whether the lockfile changed during installation | Can't efficiently compare installs based on lockfile changes |
| `measurementSource` | Scalar / string | No | How install duration was measured | Could incorrectly aggregate measurements with different semantics |
| `npmTimers` | Nested map/object | No | Individual npm phase/package timing measurements | Timer-level analysis becomes difficult |
| `prebundled` | Scalar / boolean, **tri-state** | No | Vite dependency prebundling state: `true`, `false`, or unknown/absent | Could incorrectly turn unknown into `false` |
| `domContentLoadedMs` | Scalar / number | No | Browser DOM-content-loaded timing for `clientready` | Can't efficiently compare client readiness performance |
| `firstContentfulPaintMs` | Scalar / number | No | Browser FCP timing during `clientready` | Can't efficiently compare client rendering/readiness performance |
| `spooledAt` | Scalar / integer | No | When the event was written to the local spool | Can't reliably distinguish event time from delayed delivery |
