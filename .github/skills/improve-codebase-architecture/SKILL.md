---
name: improve-codebase-architecture
description: "Scan Roamly for architecture-deepening opportunities and present a visual HTML report before grilling the chosen candidate."
---

# Improve Codebase Architecture
> User-invoked skill: run it only when the user explicitly asks for it.

Surface architectural friction in Roamly and propose deepening opportunities: refactors that turn shallow modules into deeper ones. The goal is testability, locality, leverage, and AI-navigability without violating the existing Vertical Slice Architecture, modular monolith boundaries, or ADRs.

Use this design vocabulary consistently:

- **module** - a cohesive piece of the system with a meaningful interface and implementation.
- **interface** - the surface other modules depend on; also the preferred test surface.
- **implementation** - complexity hidden behind the interface.
- **depth** - how much implementation value sits behind a small interface.
- **deep** - small interface, valuable implementation.
- **shallow** - interface nearly as complex as implementation.
- **seam** - a place behavior can be tested or adapted.
- **adapter** - concrete implementation behind a seam.
- **leverage** - many call sites gain value from one improved module.
- **locality** - related behavior and bugs are concentrated in one place.

Do not drift into generic terms when the architecture term is available.

## Roamly inputs

Read first:

- `AGENTS.md` and the orchestrator rules it references.
- `docs/architecture/CONTEXT.md` for domain language.
- ADRs under `docs/adr/`, especially in the area being reviewed.
- Existing module structure for Identity, Campers, Maintenance, Documents, Trips, Places, Expenses, Checklists, Journal, and Intelligence.

Use the custom Roamly agents when useful:

- Archimedes for architecture candidates.
- Solomon for vertical-slice and clean-code friction.
- Oracle for EF Core, query, migration, and SQL Server friction.
- Hermes for REST/OpenAPI contract friction.
- Argus for testability and regression seams.
- Sentinel for security or authorization coupling.
- Pixel for frontend UX structure.
- Janus for final production-readiness risks.

Respect the Decision Gate: if a candidate implies multiple valid MEDIUM/HIGH strategies, present the options and mark one RECOMMENDED instead of choosing silently.

## Process

### 1. Explore

Explore organically. Do not apply rigid metrics as a substitute for understanding. Notice where comprehension is expensive:

- Does understanding one Camper, Trip, Maintenance, or Expense concept require bouncing through many tiny modules?
- Are vertical slices shallow: command/query, validator, handler, endpoint, DTO, and tests all repeating the same shape with little hidden implementation?
- Are pure functions extracted only for testability while the real bugs hide in orchestration with poor locality?
- Do module seams leak EF Core entities, DTOs, authorization rules, or frontend state across unrelated areas?
- Are React routes or TanStack Query hooks duplicating business rules that should live behind backend behavior?
- Are EF Core queries, includes, projections, and migrations scattered so Oracle would flag performance or schema risk?
- Are tests forced to mock too many internals instead of hitting one meaningful interface?

Apply the deletion test to suspected shallow modules: would deleting the module concentrate complexity, or just move text around? If deletion concentrates complexity, the module may be valuable. If deletion only removes ceremony, it may be shallow.

### 2. Present candidates as an HTML report

Create a self-contained HTML report using the format in [HTML-REPORT.md](HTML-REPORT.md). Keep it outside normal production source changes when possible, for example in the Copilot session artifact folder. If a repo-local location is necessary, use a clearly throwaway untracked path such as `.scratch\architecture-review-<timestamp>.html` and do not commit it unless the user explicitly asks.

The report uses Tailwind and Mermaid from CDNs and may include handcrafted CSS/SVG diagrams. Each candidate needs a before/after visualization. Be visual; do not bury the review in prose.

For each candidate, render a card with:

- **Files/modules** - involved modules and representative files.
- **Problem** - why the current architecture causes friction.
- **Solution** - what would change in plain English.
- **Benefits** - locality, leverage, and testing improvements.
- **Before / After diagram** - side-by-side deepening visualization.
- **Recommendation strength** - `Strong`, `Worth exploring`, or `Speculative`.
- **ADR/Decision Gate callout** - only when the candidate conflicts with an ADR or requires user choice.

Good candidate examples:

- Collapse repeated TripStop timeline rules into one deeper Trip timeline module with one testing interface.
- Move duplicated MaintenanceItem recurrence calculations out of React display logic and handler glue into a domain module.
- Replace repeated Expense projection code with one query interface that hides EF Core include/projection decisions.
- Concentrate camper document authorization in one seam rather than scattering checks across endpoints.

End with **Top recommendation**: which candidate to explore first and why.

After writing the report, open it for the user if the environment allows and give the absolute path. Then ask: "Which of these would you like to explore?"

### 3. Grilling loop

Once the user chooses a candidate, use `grill-with-docs` to walk the design tree:

- What problem must the deeper module solve?
- What interface should survive as the test surface?
- Which implementation details move behind it?
- Which adapters are real, and which are hypothetical?
- Which tests survive, move, or disappear?
- Does the change need Archimedes, Oracle, Hermes, Argus, Sentinel, Pixel, or Janus input?

Update docs inline only when decisions land:

- If a deepened module names a concept missing from `docs/architecture/CONTEXT.md`, add or refine the term.
- If the user rejects a candidate for a durable reason, offer an ADR so future architecture reviews do not re-suggest it.
- If there are several valid module interfaces, apply the Decision Gate instead of choosing autonomously.

Do not implement the refactor as part of this skill unless the user explicitly turns the selected candidate into implementation work.
