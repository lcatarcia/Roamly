---
name: implement
description: Implement a Roamly feature or bug fix. Use when the user wants to build something specific with a clear spec, change a vertical slice, add API/UI behavior, or complete a GitHub Issue.
---

# Implement

Implement features and fixes using Roamly's existing architecture: ASP.NET Core, Vertical Slice Architecture, CQRS-style requests and handlers, FluentValidation, EF Core, React + Vite + TypeScript, and tests appropriate to the risk.

## Process

1. Read `docs/architecture/CONTEXT.md` and relevant ADRs before naming domain concepts.
2. Understand the existing slice, module, endpoint, UI route, and tests before writing code.
3. Identify the public seam: endpoint, command/query, handler, domain method, or React route/component.
4. If behavior is complex, risky, or a regression, use the [tdd](../tdd/SKILL.md) skill.
5. Build the smallest complete vertical slice: request/response, validator, handler, persistence/query, endpoint/OpenAPI, and frontend integration only as required.
6. Follow existing project conventions before introducing new patterns.
7. Run the smallest test/build command that covers the change; widen only when failures or risk require it.
8. Refactor only after tests are green.

## Roamly implementation discipline

- Keep Command/Query, Handler, Validator, Response, and endpoint mapping colocated per feature when the existing code does so.
- Prefer explicit request/response contracts over exposing EF Core entities.
- Keep EF Core query details inside the slice or data layer; avoid leaking persistence decisions into API/UI code.
- Use GitHub Issues as the source for issue context when an issue is referenced.
- Update OpenAPI-visible contracts, docs, and tests when behavior changes.
- Avoid broad refactors unless the user asked for them or they are required to make the current change safe.

## Roamly agent alignment

Feature implementation is owned by **Solomon**. Data questions go to **Oracle**, API contract questions to **Hermes**, tests to **Argus**, security to **Sentinel**, CI/CD to **Vulcan**, UX to **Pixel**, and final production readiness to **Janus**.

When more than one valid implementation strategy exists and the choice is MEDIUM or HIGH severity, stop and use the orchestrator's **Decision Required** consult format with 4+ options, a recommendation, trade-offs, and an "Altro / proposta custom" option. Do not silently choose frameworks, slice boundaries, storage strategies, authorization behavior, or infrastructure changes.