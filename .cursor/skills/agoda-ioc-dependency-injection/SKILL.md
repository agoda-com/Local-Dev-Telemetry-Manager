---
name: agoda-ioc-dependency-injection
description: Enforces use of Agoda.IoC attribute-based dependency injection registration in C# application projects. Classes must declare their DI lifecycle via attributes ([RegisterSingleton], [RegisterPerRequest], [RegisterTransient]) instead of centralized registration in Startup/Program. Does NOT apply to library/SDK projects. Use when writing, reviewing, or refactoring C# service classes, DI registration, Startup.cs, Program.cs, or any IoC container configuration in application projects.
---

# Agoda.IoC for Dependency Injection

## Requirement levels (RFC 2119)

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHALL**, **SHALL NOT**, **SHOULD**, **SHOULD NOT**, **RECOMMENDED**, **NOT RECOMMENDED**, **MAY**, and **OPTIONAL** in this document are to be interpreted as described in RFC 2119.

Interpretation for contributors and reviewers:
- **MUST / MUST NOT**: non-negotiable requirement (blocking)
- **SHOULD / SHOULD NOT**: strong default; deviations require explicit rationale
- **MAY / OPTIONAL**: context-dependent choice


Use [Agoda.IoC](https://github.com/agoda-com/Agoda.IoC) attribute-based registration in all C# **application** projects. Do NOT use it in **library/SDK** projects — libraries should not force a DI framework on their consumers.

## Why Attribute-Based Registration

At scale (100+ engineers, 250+ PRs/month in a single repo), centralized DI configuration creates three problems:

1. **Merge conflicts** — A single configuration class where every engineer registers services becomes a constant source of merge conflicts. At peak, Agoda's config class grew to ~4,000 lines.
2. **Hidden lifecycle** — When reading a class like `RewardsMember`, you can't tell if it's a singleton, scoped, or transient without navigating to the config file. Storing per-request user data in a singleton is a critical bug that's easy to introduce when the lifecycle isn't visible.
3. **Forgotten registrations** — Developers create a new service class but forget to register it in the container, causing runtime failures.

Attribute-based registration solves all three: registration lives on the class itself, eliminating config-file conflicts, making lifecycle immediately visible, and making it impossible to forget registration.

## When This Applies

| Project type | Use Agoda.IoC? | Reason |
|---|---|---|
| Web application | Yes | Application owns its DI container |
| API service | Yes | Application owns its DI container |
| Worker/console app | Yes | Application owns its DI container |
| Class library / SDK | **No** | Libraries must not dictate DI framework to consumers |
| NuGet package | **No** | Libraries must not dictate DI framework to consumers |

## Setup

Install the package:

```bash
dotnet add package Agoda.IoC.NetCore
```

Register in `Startup.cs` or `Program.cs`:

```csharp
services.AutoWireAssembly(new[] { typeof(Startup).Assembly }, isMockMode);
```

For multi-assembly apps, pass all application assemblies:

```csharp
services.AutoWireAssembly(new[] {
    typeof(Startup).Assembly,
    typeof(SomeOtherAppClass).Assembly
}, isMockMode);
```

## Registration Attributes

Use the attribute that matches the desired lifecycle:

| Attribute | Equivalent | When to use |
|---|---|---|
| `[RegisterSingleton]` | `services.AddSingleton<>()` | Stateless services, caches, configuration |
| `[RegisterPerRequest]` | `services.AddScoped<>()` | Per-HTTP-request state, DbContext, UoW |
| `[RegisterTransient]` | `services.AddTransient<>()` | Lightweight, stateless, short-lived |

### Basic Registration

```csharp
public interface IOrderService { }

[RegisterSingleton]
public class OrderService : IOrderService { }
```

### Multiple Interfaces

When a class implements multiple interfaces, specify which to register with `For`:

```csharp
[RegisterSingleton(For = typeof(IOrderReader))]
[RegisterSingleton(For = typeof(IOrderWriter))]
public class OrderService : IOrderReader, IOrderWriter { }
```

### Factory Registration

```csharp
[RegisterSingleton(Factory = typeof(MyServiceFactory))]
public class MyService : IMyService { }

public class MyServiceFactory : IComponentFactory<IMyService>
{
    public IMyService Build(IComponentResolver c)
    {
        return new MyService(c.Resolve<IDependency>(), "config-value");
    }
}
```

### Keyed Registration

This is for use when you need a a value at run time to decide, otherwise you could just use types, and exmaple would be in a value from a database or external api response needed to decide the key.

```csharp
[RegisterSingleton(Key = "stripe")]
public class StripePaymentGateway : IPaymentGateway { }

[RegisterSingleton(Key = "paypal")]
public class PayPalPaymentGateway : IPaymentGateway { }
```

### Collection Registration

This has a very specific use case where you require ordering, otherwise you can just register against the same itnerface multiple times and dotnet DI automatically creates a collection for you.

```csharp
[RegisterSingleton(For = typeof(IPipeline), OfCollection = true, Order = 1)]
public class ValidationPipeline : IPipeline { }

[RegisterSingleton(For = typeof(IPipeline), OfCollection = true, Order = 2)]
public class ProcessingPipeline : IPipeline { }
```

## Rules

1. **Never add** `services.AddSingleton/AddScoped/AddTransient` for application service classes — use attributes instead.
2. **Never create** centralized DI configuration classes or extension methods that group registrations (e.g., `AddOrderServices()`). This recreates the problems attributes solve.
3. **Always place** the attribute directly on the concrete class, not on the interface.
4. **Always specify** `For` when a class implements more than one interface.
5. **Libraries are exempt** — library projects provide extension methods (e.g., `services.AddMyLibrary()`) as is standard in .NET.

## Review Checklist

When writing or reviewing application code, verify:

- [ ] New service classes have a registration attribute
- [ ] No manual `services.Add*<IFoo, Foo>()` calls for application services in Startup/Program
- [ ] No centralized registration classes or `Add*Services()` extension methods for application services
- [ ] Lifecycle attribute is appropriate (not using Singleton for classes holding per-request state)
- [ ] `For` is specified when a class implements multiple interfaces
- [ ] Library projects do NOT use Agoda.IoC attributes
