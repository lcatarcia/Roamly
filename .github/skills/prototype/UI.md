# UI Prototype

Generate several radically different UI variations on a single React route. The user flips between variants in the browser, chooses one, combines ideas, or rejects all of them. The losing variants are deleted.

If the question is logic or state rather than visual structure, use [LOGIC.md](LOGIC.md).

Roamly frontend context:

- React + Vite + TypeScript.
- React Router.
- TanStack Query.
- Vitest/Testing Library and Playwright for real implementation tests.
- UI domains include camper dashboards, trip planning, maintenance, documents, expenses, checklists, journal, places, and intelligence insights.

## When this is the right shape

- "What should the camper dashboard look like?"
- "Try different layouts for the trip itinerary page."
- "I want to compare maintenance alert designs before committing."
- "Should expense entry be inline, modal, or wizard-like?"
- "How should checklist execution feel on mobile?"

## Two sub-shapes

### Sub-shape A - adjustment to an existing page, preferred

Use the existing route. Variants render on the same route, gated by a `?variant=` search param. Existing data fetching, params, route loaders, auth assumptions, and TanStack Query calls stay in place. Only the rendered subtree changes.

Default to this shape whenever there is a plausible host page. Prototypes are easier to judge when surrounded by real density: header, sidebar, actual data shape, empty states, and error states.

### Sub-shape B - new throwaway page, last resort

Use only when the prototype has no sensible existing host, such as a genuinely new top-level surface.

Create a route following the existing React Router convention. Name it clearly as a prototype, for example including `prototype` in the path or filename. Use the same `?variant=` pattern.

Before using sub-shape B, check whether the design could live inside an existing Camper, Trip, Maintenance, Expense, Checklist, or Journal page. Empty routes hide design problems.

## Process

### 1. State the question and pick N

Default to three variants. More than five becomes noise.

Write one line near the prototype entry point:

```tsx
// PROTOTYPE - three variants of the trip itinerary page, switchable with ?variant=, on the existing route.
```

### 2. Generate radically different variants

Each variant must differ in structure, not just color or copy.

Hold each variant to:

- The page's real purpose.
- The available data shape.
- The project's existing styling/component conventions.
- Clear exported component names such as `VariantA`, `VariantB`, `VariantC`.

Examples of real structural disagreement:

- Camper dashboard A: maintenance-first alert stack.
- Camper dashboard B: trip timeline as the main spine.
- Camper dashboard C: document/compliance readiness board.

Examples that are not enough:

- Same cards with different colors.
- Same grid with headings moved.
- Same form with button text changed.

### 3. Wire variants through URL state

Use React Router search params or navigation utilities already used in the app.

```tsx
const [searchParams, setSearchParams] = useSearchParams();
const variant = searchParams.get("variant") ?? "A";

return (
  <>
    {variant === "A" && <VariantA data={data} />}
    {variant === "B" && <VariantB data={data} />}
    {variant === "C" && <VariantC data={data} />}
    <PrototypeSwitcher
      variants={["A", "B", "C"]}
      current={variant}
      onChange={(next) => setSearchParams({ variant: next })}
    />
  </>
);
```

For sub-shape A, keep existing data fetching above the switch. For sub-shape B, the throwaway route owns any mock data needed to judge the design.

### 4. Build the floating switcher

A fixed bottom-center bar with:

- Left arrow - previous variant, wrapping around.
- Variant label - key and optional name, e.g. `B - Timeline spine`.
- Right arrow - next variant, wrapping around.

Behavior:

- Arrow clicks update the URL search param so the variant is shareable and reload-stable.
- Keyboard left/right arrows cycle variants.
- Do not intercept arrow keys when an input, textarea, select, or contenteditable element is focused.
- Visually distinct from the page: high-contrast pill, subtle shadow, clearly a prototype control.
- Hidden in production builds, for example behind `import.meta.env.DEV`.

Put the switcher in one shared prototype component so it can be deleted in one pass.

### 5. Hand it over

Give the URL and available variants. The valuable feedback is often mixed:

- "Use the header from B with the right rail from C."
- "A works on desktop, C works on mobile."
- "The maintenance-first layout is right, but the document warning from B is better."

### 6. Capture the answer and clean up

Once a variant wins:

- Record which idea won and why in the GitHub Issue, PRD, ADR, commit message, or a short temporary note if the user is unavailable.
- Delete losing variants.
- Delete the switcher.
- Fold the winning design into real production code with proper validation, error states, accessibility, and tests.

Sub-shape A: replace the prototype subtree on the existing page.

Sub-shape B: promote the winner to a real route and delete the throwaway route.

## Anti-patterns

- Variants that differ only in color, spacing, or copy.
- Sharing so much layout code that variants cannot disagree structurally.
- Wiring prototype controls to real mutations.
- Shipping the floating switcher.
- Treating prototype code as tested production implementation.
- Leaving variants in the repo after the decision is made.
