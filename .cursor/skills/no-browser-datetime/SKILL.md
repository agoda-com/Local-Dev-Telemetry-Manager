---
name: no-browser-datetime
description: Enforces that Date objects and datetime types are never used in browser/frontend code. Dates must be converted to strings on the server and passed as strings. Use when writing or reviewing frontend code that involves dates, timestamps, calendars, date pickers, or API responses containing dates.
---

# No DateTime in the Browser

## Requirement levels (RFC 2119)

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHALL**, **SHALL NOT**, **SHOULD**, **SHOULD NOT**, **RECOMMENDED**, **NOT RECOMMENDED**, **MAY**, and **OPTIONAL** in this document are to be interpreted as described in RFC 2119.

Interpretation for contributors and reviewers:
- **MUST / MUST NOT**: non-negotiable requirement (blocking)
- **SHOULD / SHOULD NOT**: strong default; deviations require explicit rationale
- **MAY / OPTIONAL**: context-dependent choice


## Rule

**Never use `Date` objects or datetime types in browser/frontend code.**

All date/time conversion and formatting must happen on the server. The frontend receives and works with **strings only**.

## Date String Format

When dates need to be handled programmatically in the browser (e.g. calendar selection, date pickers, comparisons), use the string format:

```
YYYY-MM-DD
```

Examples: `"2026-03-22"`, `"2025-01-01"`

For datetime values that include time, use:

```
YYYY-MM-DD HH:mm:ss
```

## What To Do

| Scenario | Approach |
|----------|----------|
| Displaying a date | Server sends a pre-formatted display string (e.g. `"March 22, 2026"`) |
| Date picker / calendar | Use `"YYYY-MM-DD"` strings; send the selected string back to the server |
| Sorting by date | Sort `"YYYY-MM-DD"` strings lexicographically (this format sorts correctly as strings) |
| Comparing dates | Compare `"YYYY-MM-DD"` strings directly |
| Date arithmetic | Send the string to the server; let the server compute and return the result |
| API request with date | Pass the `"YYYY-MM-DD"` string as-is |
| API response with date | Server must return dates as pre-formatted strings, not ISO timestamps for the client to parse |

## Prohibited Patterns

```typescript
// BAD — constructing Date objects in the browser
const d = new Date();
const d = new Date("2026-03-22");
const d = new Date(timestamp);

// BAD — using Date methods in the browser
date.toLocaleDateString();
date.getFullYear();
Date.now();
Date.parse(str);

// BAD — date libraries in frontend bundles
import dayjs from "dayjs";
import { format } from "date-fns";
import moment from "moment";
```

## Correct Patterns

```typescript
// GOOD — server sends display-ready strings
interface Event {
  title: string;
  date: string;        // "YYYY-MM-DD"
  displayDate: string;  // "March 22, 2026" — ready to render
}

// GOOD — date picker works with string values
const [selectedDate, setSelectedDate] = useState<string>("2026-03-22");

// GOOD — comparing date strings directly
const isAfter = dateA > dateB; // works because YYYY-MM-DD sorts lexicographically

// GOOD — sending date string to the server for computation
const response = await fetch(`/api/next-business-day?from=${selectedDate}`);
```

## Server Responsibility

The server must:
1. Convert all dates to the agreed string formats before sending to the client
2. Accept `"YYYY-MM-DD"` strings from the client
3. Handle all timezone conversion, date arithmetic, and locale-specific formatting
