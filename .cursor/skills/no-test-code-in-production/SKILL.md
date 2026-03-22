---
name: no-test-code-in-production
description: Enforces that mocks, stubs, fakes, and other test-only code are never placed in production source files. Use when writing, reviewing, or refactoring code that involves mocking, test doubles, dependency injection, or environment-specific configuration.
---

# No Test Code in Production

## Core Rule

Never place mocks, stubs, fakes, spy implementations, or any test-only code in production source files. Test doubles belong exclusively in test files and test directories.

## What Counts as Test Code

- Mock or fake implementations of interfaces/classes (e.g., `FakeUserRepository`, `MockHttpClient`)
- Conditional logic that checks for a test environment to swap behavior (e.g., `if (env === 'test') { ... }`)
- Test data factories or fixture builders
- Imports of test frameworks or assertion libraries
- In-memory substitutes created solely for testing (e.g., `InMemoryQueue`)

## Allowed Exception: Environment-Specific Config Files

Projects often have separate configuration files per environment. A dedicated test/dev config file is acceptable:

```
config/
├── config.production.json
├── config.staging.json
├── config.development.json
└── config.test.json        # ✅ This is fine
```

The key distinction: a **config file** that sets values for a test environment is not test code. A **source file** that contains mock implementations is.

## Preferred Approach: Dependency Injection

Instead of embedding mocks in production code, inject dependencies so tests can substitute their own implementations:

### Do This

Define abstractions in production code. Inject real implementations at runtime and test doubles in tests.

```
// Production: define the contract
interface NotificationService { send(msg): Promise<void> }

// Production: real implementation
class EmailNotificationService implements NotificationService { ... }

// Test file only: mock implementation
class MockNotificationService implements NotificationService { ... }
```

### Don't Do This

```
// ❌ Production file with a test-only class
class MockNotificationService implements NotificationService {
  async send(msg) { /* no-op for tests */ }
}

// ❌ Runtime check to swap in test behavior
function getNotificationService() {
  if (process.env.NODE_ENV === 'test') {
    return new MockNotificationService();
  }
  return new EmailNotificationService();
}
```

## Review Checklist

When writing or reviewing code, verify:

- [ ] No mock/fake/stub classes exist in production source directories
- [ ] No `if test/dev` branching to swap in test doubles at runtime
- [ ] Dependencies are injected, not hard-coded with test fallbacks
- [ ] Test-only imports (e.g., `jest`, `unittest.mock`, `Moq`) do not appear in production files
- [ ] Environment-specific configs are in dedicated config files, not inline conditionals
