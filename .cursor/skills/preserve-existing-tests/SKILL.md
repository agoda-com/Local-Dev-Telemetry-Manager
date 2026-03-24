---
name: preserve-existing-tests
description: Prevents modification of existing tests and test data unless behavior has explicitly changed. Existing passing tests are proof that current behavior is correct — changing them risks hiding bugs. Use when editing, refactoring, or fixing code that has associated tests, or when reviewing changes that modify test files.
---

# Preserve Existing Tests

## Requirement levels (RFC 2119)

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHALL**, **SHALL NOT**, **SHOULD**, **SHOULD NOT**, **RECOMMENDED**, **NOT RECOMMENDED**, **MAY**, and **OPTIONAL** in this document are to be interpreted as described in RFC 2119.

Interpretation for contributors and reviewers:
- **MUST / MUST NOT**: non-negotiable requirement (blocking)
- **SHOULD / SHOULD NOT**: strong default; deviations require explicit rationale
- **MAY / OPTIONAL**: context-dependent choice


## Core Rule

**Never modify existing tests or test data.** If existing tests fail after your changes, that is a signal you may have introduced a bug — not that the tests are wrong.

Only modify an existing test when the user has explicitly stated that the behavior under test should change.

## Why This Matters

Existing passing tests are the specification for current correct behavior. Changing a test to make it pass after a code change silently redefines "correct" and can mask regressions.

## Decision Flow

```
Existing test fails after your code change
│
├── Was the behavior change explicitly requested?
│   ├── YES → Update the test to match the new expected behavior
│   └── NO  → Your code change likely has a bug — fix the code, not the test
│
└── Need to cover new behavior?
    └── Add a new test case — don't modify existing ones
```

## What NOT To Do

- **Don't update assertions** to match new output without confirming the output change is intentional
- **Don't delete tests** that fail after a refactor
- **Don't modify test fixtures or test data** to accommodate implementation changes
- **Don't weaken test conditions** (e.g., changing exact match to contains, loosening thresholds)
- **Don't rename or restructure tests** as part of an unrelated change

## What To Do

- **Add new test cases** for new behavior alongside existing tests
- **Fix production code** when existing tests break unexpectedly
- **Ask the user** if you're unsure whether a behavior change is intentional
- **Keep existing assertions intact** even when adding new ones

## Examples

### Refactoring — Tests Must Still Pass As-Is

If you refactor a function's internals without changing its contract, all existing tests must pass without modification. A failing test means the refactor changed observable behavior.

### Adding a Feature — Add New Tests

When adding a new feature to an existing module, write new test cases for the new behavior. Do not alter existing test cases for the module.

### Bug Fix — Verify Against Existing Tests

When fixing a bug, existing tests should continue to pass. Add a new test that reproduces the bug and verifies the fix.

## Review Checklist

When reviewing changes that include test file modifications:

- [ ] Is each test modification justified by an explicitly requested behavior change?
- [ ] Are existing assertions preserved, not weakened or removed?
- [ ] Are new behaviors covered by new test cases rather than modified existing ones?
- [ ] Is test data/fixture modification necessary, or is the production code the real problem?
