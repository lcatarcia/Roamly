# Refactor Candidates

After a TDD cycle, look for:

- **Duplication**: extract builders, value objects, or shared domain rules.
- **Long handlers**: move pure domain decisions behind a small public method; keep tests at the slice seam.
- **Shallow modules**: combine pass-through services that only forward data.
- **Feature envy**: move behavior to the aggregate or value object that owns the data.
- **Primitive obsession**: introduce `CamperId`, `TripId`, `Money`, `OdometerReading`, `DateRange`, or similar value objects when they clarify invariants.
- **Leaky EF details**: keep query shape and persistence concerns inside the slice or data adapter.
- **Frontend state sprawl**: move server state to TanStack Query and keep local state for actual UI state.
- **Existing code revealed by the change**: note it, but fix only if tightly coupled to the current behavior or approved separately.

Refactor only while green, and re-run the smallest relevant test after each meaningful step.