---
name: react-component-layout
description: Enforces that React component code mirrors the visual layout of the UI. Code structure should reflect UI structure so a developer can see the same borders and divisions in the code as on screen. Use when writing, reviewing, or refactoring React components, page layouts, or UI composition.
---

# React Component Layout

Structure React code so that reading it feels like looking at the UI. A developer should be able to glance at the JSX and immediately see the same major sections, divisions, and hierarchy that appear on screen.

## Core Principles

### 1. Code Mirrors the UI

Organize JSX so its nesting and grouping match the visual layout. If the UI has a header, a sidebar, and a main content area, those should be obvious top-level blocks in the JSX — not buried inside conditionals or abstracted away at the page level.

```tsx
function DashboardPage() {
  return (
    <PageShell>
      <DashboardHeader />

      <div className="dashboard-body">
        <DashboardSidebar />
        <DashboardContent reports={reports} />
      </div>

      <DashboardFooter />
    </PageShell>
  );
}
```

Reading this component instantly reveals: header on top, sidebar + content in the middle, footer at the bottom — exactly what the user sees.

### 2. One Top-Level Page Component Per Route

Each page gets a single component that acts as its layout blueprint. This component does **only** composition — it arranges sections, it does not contain business logic or deep markup.

```tsx
function SettingsPage() {
  return (
    <PageShell title="Settings">
      <SettingsTabs activeTab={activeTab} onTabChange={setActiveTab} />
      <SettingsPanel tab={activeTab} />
    </PageShell>
  );
}
```

The page component answers: "What are the major pieces of this screen and how are they arranged?"

### 3. Keep Components Focused

Each component should own one visually distinct region of the UI. When a component starts doing too much — handling multiple unrelated sections, mixing layout with fine-grained markup — split it.

Signs a component needs splitting:
- It renders multiple visually distinct areas that could be understood independently
- You have to scroll through unrelated markup to find what you're looking for
- The JSX nesting no longer maps to what you see on screen

### 4. Hide Complexity in Well-Named Extractions

When logic or markup grows complex, extract it into a method or component whose **name describes the UI it produces**. The name replaces the need for a comment.

**Extract render helpers for conditional or computed UI chunks:**

```tsx
function OrderSummary({ order }: Props) {
  return (
    <Card>
      <OrderLineItems items={order.items} />
      <PricingBreakdown subtotal={order.subtotal} tax={order.tax} />
      {order.discount && <AppliedDiscount discount={order.discount} />}
      <OrderTotal total={order.total} />
    </Card>
  );
}
```

Not:

```tsx
function OrderSummary({ order }: Props) {
  return (
    <Card>
      {/* line items section */}
      <div className="line-items">
        {order.items.map(item => (
          <div key={item.id} className="line-item">
            <span>{item.name}</span>
            <span>{item.qty} × {item.price}</span>
          </div>
        ))}
      </div>
      {/* pricing */}
      <div className="pricing">
        <div>Subtotal: {order.subtotal}</div>
        <div>Tax: {order.tax}</div>
      </div>
      {/* ... more inline markup ... */}
    </Card>
  );
}
```

The first version reads like a description of the UI. The second requires comments to navigate.

### 5. Names Are the Documentation

Component and function names should describe **what the user sees**, not implementation details. If the name is clear, no comment is needed.

| Bad | Good |
|-----|------|
| `renderSection2` | `BillingAddressForm` |
| `handleClick` | `submitPayment` |
| `DataDisplay` | `RevenueChart` |
| `getItems` | `buildNavigationLinks` |
| `InfoBox` | `ShippingEstimate` |

Ask: "If I read just the names in the JSX, do I know what the screen looks like?" If not, rename.

## Applying These Principles

When writing or reviewing React components:

1. **Start from the page level.** Write the top-level page component first as a layout skeleton of named sections.
2. **Check the mirror.** Read the JSX — does the nesting match the visual hierarchy? Would someone unfamiliar with the code recognize the UI from reading it?
3. **Extract, don't inline.** When markup grows beyond a focused visual region, extract a component or helper named after what it renders.
4. **Name before you comment.** If you're tempted to add a comment explaining a section of JSX, extract it into a well-named component instead.
5. **Keep page components thin.** They compose sections — they don't contain implementation detail like API calls, complex state, or deep markup trees.
