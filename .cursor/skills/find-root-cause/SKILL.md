---
name: find-root-cause
description: Enforces root-cause analysis before fixing bugs or resolving errors. Patches that mask symptoms without understanding the underlying cause are not acceptable. Use when fixing bugs, resolving errors, debugging failures, or addressing unexpected behavior.
---

# Find the Root Cause

## Core Rule

**Never patch symptoms. Always find and fix the root cause.** Before writing any fix, trace the problem to its origin. A fix that suppresses the symptom without addressing why it happened will fail again — or worse, hide the real issue until it causes broader damage.

## Decision Flow

```
Bug or error reported
│
├── 1. Reproduce the problem
│   └── Can you reliably trigger it?
│       ├── YES → Proceed to step 2
│       └── NO  → Investigate further before writing any code
│
├── 2. Trace to the origin
│   └── Ask: "Why does this happen?" repeatedly until you reach
│       a cause that, if fixed, prevents the problem entirely
│
├── 3. Verify the root cause
│   └── Does fixing this one thing eliminate the symptom
│       without needing additional workarounds?
│       ├── YES → This is the root cause — implement the fix here
│       └── NO  → You're still looking at a symptom — keep digging
│
└── 4. Fix and confirm
    ├── Fix the root cause
    ├── Verify the original symptom is gone
    └── Check for related symptoms that should also be resolved
```

## What NOT To Do

- **Don't add null checks to silence a crash** without understanding why the value is null
- **Don't wrap code in try/catch to swallow errors** without understanding why the error occurs
- **Don't add special-case `if` branches** to handle a scenario that shouldn't exist
- **Don't add retry logic** to mask a flaky operation without understanding the failure mode
- **Don't add default/fallback values** to cover for data that should have been present
- **Don't move on after the symptom disappears** — confirm *why* the fix works

## What To Do

- **Read the stacktrace and trace the call chain** — don't stop at the line that throws
- **Ask "why" at least three times** to move past symptoms to causes
- **Check the data flow** — where does the bad value originate, not just where it's consumed
- **Look at recent changes** — what changed that could have introduced this?
- **Search for similar patterns** — if this bug exists here, does the same mistake exist elsewhere?
- **State the root cause explicitly** before proposing a fix

## Red Flags: You're Patching, Not Fixing

| Pattern | What It Suggests |
|---------|-----------------|
| Adding `?? defaultValue` or `\|\| fallback` at the crash site | The real question is why the value is missing upstream |
| Wrapping in `try { } catch { }` with no meaningful handling | The error's cause is being ignored |
| `if (x !== undefined && x !== null)` guard added to one call site | Something upstream should guarantee `x` exists |
| "It works now" without explaining why | The root cause is unknown — it will recur |
| Fix is in a different module than where the bug originates | The origin wasn't traced far enough |

## Examples

### Symptom: `TypeError: Cannot read property 'name' of undefined`

**Patch (wrong):**
```
const name = user?.name ?? "Unknown";
```
This silences the crash but hides that `user` is unexpectedly undefined.

**Root-cause approach:**
1. Why is `user` undefined? — The API call returned `null` for this user ID
2. Why did the API return `null`? — The user was deleted but still referenced in a session
3. Root cause: Sessions are not invalidated when a user is deleted
4. Fix: Invalidate sessions on user deletion. Add a new test for this case.

### Symptom: Flaky test that passes on retry

**Patch (wrong):**
```
jest.retryTimes(3);
```

**Root-cause approach:**
1. Why does it fail intermittently? — A race condition between async operations
2. Why is there a race? — Two operations share mutable state without synchronization
3. Root cause: Missing `await` on a setup step
4. Fix: Add the missing `await`. The test now passes reliably every time.

## Review Checklist

When fixing a bug or resolving an error, verify:

- [ ] The root cause has been explicitly identified and stated
- [ ] The fix addresses the origin of the problem, not the point where the symptom appears
- [ ] No catch-all error suppression, fallback values, or guard clauses were added solely to mask the symptom
- [ ] Similar patterns elsewhere in the codebase have been checked for the same issue
- [ ] The original symptom is confirmed resolved by the fix
