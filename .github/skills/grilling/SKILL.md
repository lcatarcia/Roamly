---
name: grilling
description: Relentlessly interview the user about a plan, feature, design, architecture choice, or Roamly workflow before building; use for grill, stress-test, poke holes, challenge this plan, or design review prompts.
---

# Grilling

Interview the user relentlessly about every aspect of the plan until we reach shared understanding. Walk the decision tree one branch at a time, resolving dependencies between decisions before moving on.

## Operating rules

- Ask one question at a time, then wait for the answer. Multiple questions at once are bewildering and make tradeoffs blurry.
- For each question, provide your recommended answer, explain why, and call out when the choice is LOW, MEDIUM, or HIGH severity under Roamly's orchestrator rules.
- If several valid implementation strategies exist, use the Decision Gate: present at least four concrete options, mark one RECOMMENDED, include pros, cons, architectural impact, complexity, and an "Other / custom proposal" option. Do not choose autonomously for MEDIUM or HIGH decisions.
- If a question can be answered by inspecting Roamly's codebase or docs, inspect them instead of asking. Prefer docs under `docs/architecture`, `docs/product`, and `docs/adr`, then relevant backend/frontend code.
- Anchor questions in Roamly's stack: ASP.NET Core, EF Core, SQL Server, Vertical Slice Architecture, CQRS, FluentValidation, React, TanStack Query, Vitest, Playwright, Docker, GitHub Actions.
- Consider which Roamly agent should be consulted for the subject: Archimedes for architecture, Solomon for clean vertical slices, Oracle for data, Hermes for API, Argus for tests, Sentinel for security, Vulcan for CI/CD, Pixel for UX, Janus for final readiness, Scribe for history.

## Completion criterion

Continue until every important dependency, risk, unknown, and decision has either been resolved, delegated to a prototype/research step, or explicitly parked with a rationale. End with a compact summary of agreed decisions, unresolved decisions, recommended next skill, and any agent that should be involved.
