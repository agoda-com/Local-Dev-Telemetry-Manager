---
name: csharp-testing-strategy
description: Enforces the Honeycomb testing strategy for C# services. Prefer black-box integration tests using WebApplicationFactory over unit tests. Unit tests are only for isolated helpers. Use when writing, reviewing, or refactoring tests in C# projects, or when the user asks how to test a feature, endpoint, service, or handler.
---

# C# Testing Strategy

This skill follows the **Honeycomb testing model**: the bulk of tests should be integration tests that exercise real behavior through the HTTP boundary, not unit tests of internal classes. Unit tests are reserved for pure helper/utility methods with no dependencies.

## Test Pyramid vs Honeycomb

The Honeycomb model inverts the traditional test pyramid. Instead of many unit tests and few integration tests, the distribution is:

| Layer | Volume | What belongs here |
|-------|--------|-------------------|
| **Integration (black-box)** | Most tests | Endpoints, handlers, business workflows — tested via `WebApplicationFactory` |
| **Unit** | Few tests | Pure functions, extension methods, mappers, validators with no I/O |
| **E2E / Contract** | As needed | Cross-service contract verification (Pact) |

**Default stance**: if you're about to write a unit test for a service, repository, or handler — stop. Write a black-box integration test instead.

## Black-Box Integration Tests

### Use WebApplicationFactory

All integration tests start the app in-process using `WebApplicationFactory<Program>` and issue real HTTP requests. Tests assert on HTTP responses, not internal state.

```csharp
[TestFixture]
public class OrderTests : IntegrationTestBase
{
    [Test]
    public async Task PlaceOrder_WithValidItems_ReturnsCreated()
    {
        var request = new PlaceOrderRequest { Items = [new("SKU-1", 2)] };

        var response = await Client.PostAsJsonAsync("/api/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<OrderResponse>();
        body!.Items.Should().HaveCount(1);
    }
}
```

### What to assert

- HTTP status codes
- Response body shape and values
- Side effects observable through the API (e.g., GET after POST)
- Headers when relevant (Location, Content-Type)

### What NOT to assert

- Internal service method calls
- Repository interactions
- Private state

## When to Use Unit Tests

Unit tests are appropriate **only** for code that is:
- A pure function (deterministic, no side effects)
- Not wired into DI or the HTTP pipeline
- Complex enough that edge cases are hard to cover through integration tests

Examples: string formatters, calculation helpers, custom validators, mapping extensions.

## External Dependency Strategy

Choose the mock approach based on the dependency type:

### Databases and Queues → TestContainers

Use Testcontainers to spin up real instances. This catches schema mismatches, query bugs, and serialization issues that in-memory fakes hide.

```csharp
private static readonly PostgreSqlContainer Postgres = new PostgreSqlBuilder()
    .WithImage("postgres:16-alpine")
    .Build();

[OneTimeSetUp]
public async Task OneTimeSetUp() => await Postgres.StartAsync();

[OneTimeTearDown]
public async Task OneTimeTearDown() => await Postgres.DisposeAsync();
```

Register the real connection string in `WebApplicationFactory.ConfigureWebHost`.

### Cross-Cutting APIs → In-Code Mocks

For dependencies that rarely change their contract — auth, translations, feature flags, experimentation — mock them in-process by replacing the service registration in WebApplicationFactory.

```csharp
builder.ConfigureTestServices(services =>
{
    services.RemoveAll<IAuthClient>();
    services.AddSingleton<IAuthClient>(new FakeAuthClient(authenticatedUserId: "user-42"));

    services.RemoveAll<ITranslationService>();
    services.AddSingleton<ITranslationService>(new StubTranslationService());
});
```

These contracts are stable. In-code mocks are fast, simple, and sufficient.

### Line-of-Business External APIs → External HTTP Mocks

For APIs owned by other teams whose contracts change more frequently, use **WireMock.Net** (or **Pact** when the infrastructure is already set up). External HTTP mocks catch serialization issues, status code handling, and contract drift that in-code mocks would miss.

#### WireMock.Net — Fluent Setup Only

**Do not use JSON mapping files.** Use the fluent C# API so mocks are co-located with the test, type-checked, and reviewable in diffs.

```csharp
private WireMockServer _pricingApi = null!;

[SetUp]
public void SetUp()
{
    _pricingApi = WireMockServer.Start();

    _pricingApi
        .Given(Request.Create()
            .WithPath("/api/prices")
            .WithParam("sku", "SKU-1")
            .UsingGet())
        .RespondWith(Response.Create()
            .WithStatusCode(200)
            .WithBodyAsJson(new { sku = "SKU-1", price = 29.99 }));
}

[TearDown]
public void TearDown() => _pricingApi.Stop();
```

Point the app at the WireMock URL by overriding configuration in `WebApplicationFactory.ConfigureWebHost`.

#### Pact (When Infrastructure Exists)

If your team already has Pact broker infrastructure, prefer Pact for consumer-driven contract tests against line-of-business APIs. Do not set up Pact infrastructure from scratch just for one test — use WireMock instead.

## Decision Flowchart

```
Need to test something?
│
├─ Is it a pure helper/utility with no dependencies?
│  └─ YES → Unit test
│
└─ NO → Black-box integration test via WebApplicationFactory
         │
         ├─ DB or queue dependency?
         │  └─ TestContainers
         │
         ├─ Cross-cutting API (auth, translations, feature flags)?
         │  └─ In-code mock (replace in DI)
         │
         └─ Line-of-business external API?
            ├─ Pact infra exists? → Pact
            └─ Otherwise → WireMock.Net (fluent, no JSON)
```

## Common Mistakes

- **Writing unit tests for controllers/handlers** — test through HTTP instead.
- **Using in-memory DB providers** (e.g., EF InMemory) — use TestContainers for real DB behavior.
- **Mocking internal services to test a feature** — this tests implementation, not behavior. Use WebApplicationFactory.
- **WireMock JSON mapping files** — they drift from code, are hard to review, and bypass compile-time checks. Use the fluent API.
- **Over-mocking cross-cutting concerns with WireMock** — these rarely change contracts. In-code mocks are simpler and faster.
