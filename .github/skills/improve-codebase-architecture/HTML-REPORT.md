# HTML Report Format

The architecture review is rendered as a single self-contained HTML file. Prefer a Copilot session artifact location so the repo stays clean. If a repo-local file is necessary, use a clearly throwaway untracked path such as `.scratch\architecture-review-<timestamp>.html` and do not commit it unless the user asks.

Tailwind and Mermaid come from CDNs. Mermaid handles graph-shaped diagrams reliably; handcrafted CSS/SVG handles editorial visuals such as mass diagrams and cross-sections. Mix them.

## Scaffold

```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <title>Architecture review - Roamly</title>
    <script src="https://cdn.tailwindcss.com"></script>
    <script type="module">
      import mermaid from "https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs";
      mermaid.initialize({ startOnLoad: true, theme: "neutral", securityLevel: "loose" });
    </script>
    <style>
      .seam { stroke-dasharray: 4 4; }
      .leak { stroke: #dc2626; }
      .deep { background: linear-gradient(135deg, #0f172a, #1e293b); }
    </style>
  </head>
  <body class="bg-stone-50 text-slate-900 font-sans">
    <main class="max-w-6xl mx-auto px-6 py-12 space-y-12">
      <header>...</header>
      <section id="candidates" class="space-y-10">...</section>
      <section id="top-recommendation">...</section>
    </main>
  </body>
</html>
```

## Header

Include repo name, date/time of review, comparison point if any, and a compact legend: solid box = module, dashed line = seam, red arrow = leakage, thick dark box = deep module, amber callout = ADR or Decision Gate issue. No long introduction.

## Candidate card

The diagrams carry the weight. Prose should be sparse and use the architecture vocabulary exactly.

Each candidate is one `<article>`:

- **Title** - short, names the deepening, e.g. "Deepen Trip timeline assembly".
- **Badge row** - recommendation strength and dependency category.
- **Files/modules** - monospaced list of representative files and affected modules.
- **Before / After diagram** - centerpiece, two columns side by side.
- **Problem** - one sentence.
- **Solution** - one sentence.
- **Wins** - bullets, six words or fewer when possible.
- **ADR/Decision Gate callout** - one amber-tinted note if relevant.

Recommendation badges: `Strong` = emerald, `Worth exploring` = amber, `Speculative` = slate.

Dependency categories: `in-process`, `local-substitutable`, `ports-and-adapters`, `mock-only`, `frontend-state`, `ef-core-query`, `api-contract`.

## Diagram patterns

### Mermaid graph

Use a Mermaid `flowchart`, `graph`, or `sequenceDiagram` for dependencies, call flow, endpoint-to-handler flow, or EF Core query paths.

```html
<div class="rounded-lg border border-slate-200 bg-white p-4">
  <pre class="mermaid">
    flowchart LR
      Endpoint[TripStops endpoint] --> Handler[CreateTripStop handler]
      Handler --> Validator[Trip date validator]
      Handler -.leaks.-> Timeline[Timeline assembly]
      Handler --> Db[(SQL Server)]
      classDef leak stroke:#dc2626,stroke-width:2px;
      class Handler,Timeline leak
  </pre>
</div>
```

### Hand-built boxes and arrows

Use positioned `<div>` boxes and inline SVG arrows when Mermaid fights the intended layout. This is best when the after diagram should show one thick deep module with faded internal details.

### Cross-section

Use stacked horizontal bands to show layered shallowness. Before: six thin bands named Endpoint, DTO, Validator, Handler, Query, Mapper, each doing little. After: one thick band named Trip timeline module, with internals visible but not part of the interface.

### Mass diagram

Show interface size versus implementation size. Before: the interface rectangle is nearly as large as implementation, so the module is shallow. After: interface is small, implementation is larger, so the module is deep.

### Call-graph collapse

Before: nested boxes for endpoint -> handler -> helpers -> EF query -> mapper -> frontend normalization. After: one deep module, with former calls faded inside it.

### Frontend route shape

For React/Vite routes, show before/after ownership of state: before, route/hooks/query options/form validation/UI components all know the same rules; after, route owns orchestration and a smaller interface hides query/validation details.

## Style guidance

- Editorial, not corporate dashboard.
- Generous whitespace.
- One accent color plus red for leakage and amber for warnings.
- Diagrams around 320px tall so before/after compares without scrolling.
- Use `text-xs uppercase tracking-wider` for module labels.
- The only scripts should be Tailwind and Mermaid.

## Top recommendation

One larger card: candidate name, one sentence on why it should be tackled first, anchor link to its card, and Decision Gate note if user approval is required.

## Tone

Plain English, concise, and architecture-specific.

Use exactly: module, interface, implementation, depth, deep, shallow, seam, adapter, leverage, locality.

Avoid substituting component/service/unit when you mean module, API/signature when you mean interface, boundary when you mean seam, or layer/wrapper when you mean module.

Phrasings that fit:

- "Trip timeline module is shallow - interface nearly matches implementation."
- "Maintenance recurrence leaks across the seam."
- "Deepen: one interface, one place to test."
- "Two adapters justify the seam: SQL Server in production, in-memory in tests."

Wins bullets should name the architectural gain:

- "locality: Trip bugs concentrate"
- "leverage: one interface, many callers"
- "interface shrinks; implementation absorbs ceremony"
- "Argus tests one seam"

No hedging. If a sentence could be a bullet, make it a bullet. If a bullet could be cut, cut it.
