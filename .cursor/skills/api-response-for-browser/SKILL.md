---
name: api-response-for-browser
description: Enforces efficient API endpoint design when serving data to browsers. Minimise HTTP requests per page, use view models to shape data server-side, and choose between client-side and server-side filtering based on data volume. Use when writing or reviewing API endpoints, AJAX calls, frontend data fetching, view models, or any browser-to-server data requests.
---

# API Response Design for Browser Clients

## Rule

**Shape data on the server to match what the page needs. Minimise the number of HTTP requests per page.**

Browsers enforce a parallel connection limit per domain (typically 6). Every additional request competes for those slots, increasing page load time and hurting user experience.

## Core Principles

1. **One request per page view** is the ideal. A small number (2–3) is acceptable when the data is logically independent and loaded in parallel. More than that needs justification.
2. **Use view models** — build a server-side response object that mirrors the page structure. Do not make the browser assemble its own view from multiple generic entity endpoints.
3. **Push logic to the server** — sorting, formatting, computed fields, conditional display flags. The browser should receive data that is ready to render.
4. **Aggregate related data** — if a page shows a header, a list, and summary stats, return them in a single response, not three separate calls.

## View Model Approach

Build a dedicated response model per page or component that returns exactly what the UI needs:

```csharp
// GOOD — single endpoint returns everything the page needs
public class OrderPageViewModel
{
    public OrderSummary Summary { get; set; }
    public List<OrderLineItem> Lines { get; set; }
    public CustomerInfo Customer { get; set; }
    public List<string> AvailableActions { get; set; }
}

[HttpGet("orders/{id}/page")]
public OrderPageViewModel GetOrderPage(int id) { ... }
```

```typescript
// GOOD — one fetch, one response, everything the page needs
const data = await fetch(`/api/orders/${id}/page`);
```

```csharp
// BAD — browser makes three requests to assemble the same page
[HttpGet("orders/{id}")]
[HttpGet("orders/{id}/lines")]
[HttpGet("customers/{customerId}")]
```

## Filtering Strategy

Choose the filtering approach based on data volume and filter complexity:

| Scenario | Approach |
|----------|----------|
| Small dataset, simple filters (< ~500 rows, 1–2 filter fields) | Return all data; filter client-side for instant UX |
| Moderate dataset, simple filters | Return slightly more data than the default view to allow fast client-side filtering without round-trips |
| Large dataset or complex filters (search, multi-field, range queries) | Filter server-side; browser sends filter params and receives filtered results |
| Paginated data | Always server-side; return one page at a time |

```typescript
// GOOD — small dataset, simple filter: return all, filter in browser
const [statusFilter, setStatusFilter] = useState("all");
const filtered = items.filter(i => statusFilter === "all" || i.status === statusFilter);
```

```typescript
// GOOD — large dataset, complex filter: call back to server
const results = await fetch(`/api/products?category=${cat}&minPrice=${min}&maxPrice=${max}&q=${search}`);
```

```typescript
// BAD — fetching thousands of rows to filter two fields in the browser
const allProducts = await fetch("/api/products"); // returns 50,000 rows
const filtered = allProducts.filter(p => p.price > min && p.price < max);
```

## Prohibited Patterns

- **Chatty APIs** — multiple sequential requests to load a single page view
- **Generic entity endpoints as the sole API surface for pages** — forcing the browser to join data from `/users`, `/orders`, `/products` separately
- **Returning large datasets for client-side filtering** when the filter is complex or the data exceeds a few hundred rows
- **Returning raw database models** — expose view models shaped for the UI, not ORM entities

## Correct Patterns

- One endpoint per page/view returning a composed view model
- Server-side aggregation of related data into a single response
- Pre-computed display values (formatted dates, status labels, computed totals)
- Lightweight filter params sent to the server when data volume is large
- Small over-fetch for simple client-side filters to avoid round-trips

## When Designing a New Endpoint

1. Identify the page or component that will consume the data
2. List every piece of data the page displays
3. Build a single view model that contains all of it
4. Decide filtering strategy based on expected data volume
5. Ensure the browser needs at most 1–3 requests to fully render the page
