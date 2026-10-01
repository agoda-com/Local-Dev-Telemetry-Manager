---
name: react-component-layout
description: Enforces that React component code mirrors the visual layout of the UI. Code structure should reflect UI structure so a developer can see the same borders and divisions in the code as on screen. Use when writing, reviewing, or refactoring any .tsx/.jsx component file. Trigger on: a flat components/ folder holding several areas' files, folders nested more than two levels under components/, a shared/ or common/ folder, a file that exports more than one component, a file named after a kind of thing (Cells, Rows, Buttons, helpers) rather than something on screen, a row, panel or section defined as a function inside its parent's file, .map() with more than a few lines of inline markup, conditional rendering nested inside iteration, table/list markup written inline rather than extracted into row components, {/* comment */} annotations used to label JSX sections, client-side data formatting (new Date, .toFixed, .slice) that should be a server-side view model.
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

Each component should own one visually distinct region of the UI. When a component starts doing too much — handling multiple unrelated sections, mixing data fetching with rendering, or inlining markup that belongs in a child component — split it.

Signs a component needs splitting:
- It renders multiple visually distinct areas that could be understood independently
- You have to scroll through unrelated markup to find what you're looking for
- The JSX nesting no longer maps to what you see on screen
- It fetches data or holds state that only one of its children uses

Don't judge this by line count. A long component that draws one region is fine; a short one that draws three is not.

### 4. One File, One Region

A file is named after one thing on screen and exports one component.

Pieces that only that component uses can stay in its file **as long as they are parts of it** — a toggle, a label, an icon, a variant of the same cell. A piece that is a region in its own right gets its own file, named after what it draws. A piece is a region when any of these is true:
- The user would point at it as a separate thing: a row, a panel, a section, a picker, a dialog
- It has its own private parts (a helper inside the file that composes other helpers)
- Another file needs it

```tsx
// GOOD — each region is its own file; parts stay with their region
// TelemetryPanel.tsx      → table + header row
// TestResultRow.tsx       → one result row, with a private StatusIcon
// TestResultDetails.tsx   → the expanded details panel
import TestResultRow from './TestResultRow';

export default function TelemetryPanel({ results }: Props) {
  return (
    <table>
      <tbody>
        {results.map((r) => (
          <TestResultRow key={r.id} result={r} />
        ))}
      </tbody>
    </table>
  );
}
```

```tsx
// BAD — "extracted" but every region still lives in the parent's file
function TestResultRow({ result }: { result: Result }) { /* uses ExpandedDetails */ }
function ExpandedDetails({ result }: { result: Result }) { /* ... */ }
export default function TelemetryPanel({ results }: Props) { /* ... */ }
```

Don't make files by kind. `TableCells.tsx`, `Rows.tsx`, `Buttons.tsx` or `helpers.tsx` group components because they are the same sort of thing, not because they are the same part of the screen, so a reader looking for one region has to search a grab-bag.

```
BAD                         GOOD
TableCells.tsx              OverallCell.tsx
  export OverallCell        SkillCell.tsx
  export SkillCell          TestCell.tsx   (private: TestAverageCell, TestLatestCell)
  export TestCell           ScoreCell.tsx  (shared by OverallCell and SkillCell)
  ScoreCell, ScoreDelta, …
```

Signs a file is a bucket:
- Its name is a plural or a category, not something the user sees
- It exports more than one component
- Different parents import different things from it
- A private helper inside it composes other private helpers

### 5. Folders Follow the Screen

The folder tree should read like the file tree of the UI: open `components/` and you see the app's areas; open an area and you see its regions.

- **One folder per area.** Each page or major area gets a folder named after it (`components/catalog/`, `components/benchmarks/`). Everything only that area uses lives inside it.
- **Shared components sit at the top.** A component used by more than one area stays directly in `components/`. Don't invent a `shared/` or `common/` folder; that is a bucket by another name.
- **A region with its own family gets a sub-folder.** When a high-level component inside an area has several child regions of its own (a table with its rows and cells, a panel with its tabs), give it a folder inside the area: `benchmarks/matrix/`, `catalog/install/`.
- **Stop at two levels.** `components/<area>/<region>/` is as deep as it goes. If you want a third level, the region is really its own area; lift it up.
- **No folder for one file.** A folder with a single component in it is just a longer import path.
- **Name folders after the screen, not by kind.** `benchmarks/matrix/`, not `cells/`, `rows/`, `hooks/` inside an area.
- **Keep the full component name.** JSX and imports don't show the folder, so `matrix/BenchmarkTestRow.tsx` keeps its prefix rather than becoming `matrix/TestRow.tsx`.

```
components/
  Header.tsx              shared by every page
  ErrorState.tsx          used by catalog and detail
  catalog/
    CatalogResults.tsx
    CatalogGrid.tsx
    install/              InstallPanel and its tabs
      InstallPanel.tsx
      InstallTabList.tsx
      InstallTabPanels.tsx
  benchmarks/
    BenchmarkResults.tsx
    controls/
    matrix/
```

### 6. Keep the Component Tree Shallow Too

Splitting into regions should make the tree wider, not deeper. A reader should be able to get from the page to any cell on screen in a few hops: page → area → region → part.

- **Compose at the parent.** If `A` renders `B` which only renders `C`, have `A` render `C` (or pass `C` as children). A component that only forwards props to one child is a level you don't need.
- **Siblings over chains.** A page that lists five sections side by side reads better than a page that renders one section, which renders the next, which renders the next.
- **Don't split below a part.** A badge, a label or a cell's delta arrow is a part. It can be a small function in its region's file; it doesn't need its own file or its own children.

### 7. Push Display Logic to the Server via View Models

When a component formats, truncates, or transforms API data for display — that's a sign the API response isn't shaped for the UI. Prefer pushing display formatting to the server and returning a **view model** with display-ready fields.

```tsx
// BAD — client does all the formatting
<td>{new Date(r.triggered_at).toLocaleDateString(...)}</td>
<td>{(r.duration_ms / 1000).toFixed(1)}s</td>
<td>{r.commit_sha?.slice(0, 7)}</td>

// GOOD — server returns display-ready fields
<td>{r.display_date}</td>
<td>{r.display_duration}</td>
<td>{r.short_sha}</td>
```

This reduces component complexity, eliminates client-side `Date` objects, and keeps components focused on layout — not data transformation.

### 8. Hide Complexity in Well-Named Extractions

When logic or markup grows complex, extract it into a component whose **name describes the UI it produces**. The name replaces the need for a comment.

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

### 9. Names Are the Documentation

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
3. **Extract, don't inline.** When markup grows beyond a focused visual region, extract a component named after what it renders — into its own file if it is a region, not just a part.
4. **Name before you comment.** If you're tempted to add a comment explaining a section of JSX, extract it into a well-named component instead.
5. **Check the folders.** Does `components/` show the app's areas, and each area its regions, no more than two levels deep?
6. **Keep page components thin.** They compose sections — they don't contain implementation detail like API calls, complex state, or deep markup trees.
