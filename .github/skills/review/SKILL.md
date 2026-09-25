---
name: review
description: "Review Roamly changes since a fixed point along two axes: Standards from AGENTS and architecture rules, and Spec from the GitHub Issue or PRD."
---

# Review

Review the diff between `HEAD` and a fixed point along two separate axes:

- **Standards** - does the code follow Roamly's documented engineering standards?
- **Spec** - does the code implement what the originating GitHub Issue, PRD, or brief asked for?

Keep the axes separate. A change can satisfy the spec while violating standards, or follow standards while implementing the wrong thing.

## Roamly standards sources

Standards include:

- `AGENTS.md` and the orchestrator at `C:\dev\ai-agents\orchestrator.md`.
- Vertical Slice Architecture and CQRS conventions.
- Module boundaries for Identity, Campers, Maintenance, Documents, Trips, Places, Expenses, Checklists, Journal, and Intelligence.
- FluentValidation, EF Core, SQL Server, REST/OpenAPI, xUnit, React/Vite/TypeScript, TanStack Query, Vitest/Testing Library, and Playwright conventions already present in the repo.
- Roamly agent quality rules, especially Janus production-readiness expectations.
- Any local `CONTRIBUTING`, coding standards, ADRs, or docs under `docs/architecture`, `docs/product`, and `docs/adr`.

## Process

### 1. Pin the fixed point

The fixed point is whatever the user supplies: commit SHA, branch, tag, `main`, `HEAD~5`, or merge base.

If the user did not supply one, ask for it. Do not guess.

Confirm:

```powershell
git rev-parse <fixed-point>
git diff <fixed-point>...HEAD --stat
git log <fixed-point>..HEAD --oneline
```

Use three-dot diff for review so the comparison is against the merge base:

```powershell
git diff <fixed-point>...HEAD
```

If the ref is invalid or the diff is empty, stop there.

### 2. Identify the spec source

Look in this order:

1. GitHub Issue references in commit messages, branch name, PR body, or comments (`#123`, `Closes #45`, `Fixes #67`).
2. A path supplied by the user.
3. A PRD or spec under `docs`, `docs/product`, `specs`, or a repo scratch/planning area matching the branch or feature.
4. If none exists, ask the user. If they confirm there is no spec, the Spec axis reports "no spec available" rather than inventing requirements.

The spec is the originating GitHub Issue or PRD, not the implementation's self-description.

### 3. Identify standards sources

Read the relevant standards and docs. At minimum consider `AGENTS.md`, the orchestrator rules, nearby ADRs, and any documented conventions in the touched modules.

For backend changes, check vertical-slice/CQRS shape, validation, EF Core usage, API contracts, tests, and security.

For frontend changes, check route/data ownership, TanStack Query usage, accessibility, state locality, tests, and consistency with existing UI patterns.

### 4. Run the two axes separately

Use separate contexts when available so the axes do not pollute each other. Map Roamly agents where useful:

- Standards lens: Solomon for clean code and vertical slices, Oracle for EF Core, Hermes for API, Argus for tests, Sentinel for security, Pixel for UI, Vulcan for CI/CD, and Janus for final quality gate.
- Spec lens: compare the diff strictly against the GitHub Issue/PRD/brief and acceptance criteria; involve Scribe only to recover engineering history if needed.

If reviewing directly in one session, still keep notes and output separate.

#### Standards prompt shape

Include:

- Diff command and commit list.
- Standards-source files read.
- Relevant ADRs.
- Brief: report each place the diff violates a documented standard or Roamly architecture rule. Cite the standard. Distinguish hard violations from judgment calls. Skip findings that tooling already enforces unless the diff shows an actual problem.

#### Spec prompt shape

Include:

- Diff command and commit list.
- Spec source path or fetched GitHub Issue content.
- Brief: report requirements that are missing, partial, over-scoped, or implemented incorrectly. Quote the spec or acceptance criterion for each finding. If no spec exists, say so.

### 5. Aggregate without reranking

Present the reports under two headings:

```markdown
## Standards

<findings or "No findings">

## Spec

<findings or "No findings" / "No spec available">

## Summary

Standards: <count> finding(s), worst: <short description or none>.
Spec: <count> finding(s), worst: <short description or none>.
```

Do not merge the axes or pick a single overall winner. If Janus would block production readiness, say that under Standards and in the summary.

## Standards checklist

Use this checklist as a prompt, not a substitute for reading the diff.

Backend:

- Does each change belong to the correct module?
- Is the vertical slice complete and cohesive?
- Are command/query, handler, validator, response, endpoint, and tests organized consistently with existing slices?
- Are FluentValidation rules at the right seam?
- Are EF Core queries efficient, projected appropriately, and free of obvious N+1 risk?
- Are migrations safe and intentional?
- Are REST status codes, DTOs, OpenAPI metadata, and error shapes consistent?
- Is authorization explicit and least-privilege?
- Are tests at the highest useful seam and focused on behavior?

Frontend:

- Does route state stay local and understandable?
- Are TanStack Query keys, invalidation, and loading/error states consistent?
- Are forms accessible and validation messages user-visible?
- Are Vitest/Testing Library tests behavior-focused?
- Is Playwright used only for valuable end-to-end confidence?

Cross-cutting:

- Does the change avoid hidden coupling and overengineering?
- Are docs and ADRs updated when decisions changed?
- Would Janus approve production readiness?

## Spec checklist

- Every acceptance criterion is implemented or explicitly deferred.
- Edge cases in the spec are covered.
- The diff does not add unrelated features.
- The implementation does not narrow behavior beyond what was requested.
- User-visible copy, API contract, and validation behavior match the spec.
- Tests demonstrate the promised behavior.
