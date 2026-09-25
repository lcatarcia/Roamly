---
name: which-skill
description: User-invoked router that decides which Roamly skill or project agent fits the current situation across planning, implementation, testing, review, docs, triage, teaching, and git workflows.
---

# Which Skill Fits My Situation

> User-invoked skill: run it only when the user explicitly asks for it.

Use this when the user does not know which Roamly skill or agent to use next. A skill is a repeatable workflow. An agent is specialist judgement from `.github/agents/`. Sometimes the right answer is a skill, sometimes an agent, sometimes both.

## First question

Classify the situation before routing:

1. Are we shaping an idea, building it, debugging it, reviewing it, documenting it, or maintaining the repo?
2. Is there already a codebase context and durable docs/issues, or is this a stateless conversation?
3. Is the next action a workflow skill, specialist judgement from an agent, or a Decision Gate requiring the user?
4. Is the impact LOW, MEDIUM, or HIGH under the orchestrator? MEDIUM/HIGH choices require human-in-the-loop.

## Main product flow: idea to shipped change

Use this for normal Roamly feature work.

1. `grill-with-docs` - Start here when an idea needs sharpening against Roamly docs, ADRs, product context, or existing code. It should preserve learnings in the appropriate project artifacts.
2. `decision-mapping` - Use when the discussion is mostly about tradeoffs, options, severity, and the orchestrator Decision Gate. It should produce options with one RECOMMENDED path, not silently choose.
3. `to-prd` - Use when the idea is understood well enough to become a Product Requirements Document.
4. `to-issues` - Use when a PRD must be split into GitHub Issues or agent-ready implementation slices.
5. `implement` - Use when the change is ready to build in the codebase.
6. `tdd` - Use when implementation should be driven by tests first, or when a regression needs a red test before a fix.
7. `review` - Use before handoff or PR when the work needs code review discipline.
8. Janus agent - Use as the final production-readiness gate. No feature is production-ready without Janus validation when the orchestrated agent workflow applies.

### Context hygiene

Keep grilling, decision mapping, PRD creation, and issue splitting in one coherent context when possible. If the context window is getting crowded or a new session is needed, use `handoff` rather than relying on memory.

## Bugs and unknown failures

- `diagnosing-bugs` - Use when behaviour is wrong and the cause is not known. It should trace from symptom to root cause before changing code.
- `tdd` - Use after the suspected cause is known and a regression test can capture it.
- `implement` - Use to apply the fix once the path is clear.
- Argus agent - Use for testing strategy, edge cases, regression coverage, flaky tests, and test quality.
- Solomon agent - Use when the fix requires clean vertical slice implementation or refactoring.
- Oracle agent - Use when the bug is data access, migrations, EF Core, SQL Server, or performance.
- Hermes agent - Use when the bug is API shape, OpenAPI, status codes, DTOs, or versioning.
- Sentinel agent - Use when the bug has security, authorization, authentication, or data exposure implications.
- Janus agent - Use when the bug fix may create regressions or needs final readiness review.

## Architecture and domain thinking

- `codebase-design` - Use for architecture design inside the existing codebase.
- `domain-modeling` - Use for aggregates, invariants, module boundaries, ubiquitous language, and domain concepts.
- `improve-codebase-architecture` - Use for codebase health work that identifies architectural deepening opportunities.
- Archimedes agent - Use for architecture, module boundaries, patterns, and tradeoff analysis.
- Oracle agent - Use for persistence model, query strategy, migrations, and EF Core performance.
- Janus agent - Use for cross-domain consistency and production-readiness concerns.

If there are several valid architecture strategies, stop at the Decision Gate. Do not choose autonomously.

## Prototyping and discovery

- `prototype` - Use when a runnable answer is needed: UI feel, API feasibility, business logic behaviour, or technical spike. Prototype code is disposable unless explicitly promoted.
- `grilling` - Use for one-question-at-a-time plan stress testing when project docs are not central.
- `grill-me` - User-invoked shortcut to start a `grilling` session.
- Pixel agent - Use for UX, UI flows, accessibility, and frontend consistency.
- Hermes/Oracle/Solomon agents - Use when the prototype question is specifically API, data, or implementation structure.

## Requirements, issues, and triage

- `triage` - Use for raw incoming bug reports or feature requests that need classification, reproduction, priority, and agent-ready shaping.
- `to-prd` - Use when a settled idea needs a PRD.
- `to-issues` - Use when a PRD needs implementation issues.
- Scribe agent - Use when engineering history, audit trail, merge tracking, or architecture evolution must be recorded.

Do not triage issues produced by `to-issues`; they should already be agent-ready.

## Reviews and quality gates

- `review` - Use for normal code review, maintainability, regressions, and implementation quality.
- Janus agent - Use for final quality gate and production readiness. Janus can block, request refactor, request tests, or require another specialist review. Janus does not implement.
- Sentinel agent - Use for security hardening, vulnerability analysis, least privilege, authn/authz, secret handling, and data exposure.
- Argus agent - Use for test coverage, edge cases, integration tests, and regression strategy.
- Vulcan agent - Use for GitHub Actions, Docker, deployment, logging, monitoring, and rollback.

## Merge and git workflows

- `resolving-merge-conflicts` - Use when a branch has conflicts or needs conflict-safe integration.
- `git-guardrails` - Use when the user wants rules or local hooks to prevent dangerous git operations.
- `setup-pre-commit` - Use when the user wants pragmatic pre-commit checks for Roamly's .NET + React repo.
- Vulcan agent - Use when the issue involves CI/CD, build pipeline, Docker, or deployment.
- Janus agent - Use when a merge or release has cross-cutting regression risk.

Never route autonomous work toward force push, reset hard, clean recursive deletion, or history rewrite on shared branches without explicit user approval and a Decision Gate.

## Learning and skill authoring

- `teach` - Use when the user wants to learn a concept over multiple sessions with durable lessons, resources, and learning records.
- `writing-great-skills` - Use when writing, editing, porting, pruning, or diagnosing project-level skills.
- `which-skill` - Use when the user is lost about the right route.

## Documentation and handoff

- `handoff` - Use when a thread is full, a fresh session is needed, or context must be transferred safely. It creates a compact, redacted document with suggested skills and agents.
- Scribe agent - Use when the output should become engineering history, audit trail, or durable project narrative.
- `to-prd`, `to-issues`, and `decision-mapping` - Use when documentation is not merely a summary but a requirements, issue, or decision artifact.

## Full Roamly skill set quick map

- `tdd`: test-first development, regression capture, red-green-refactor.
- `implement`: build an agent-ready issue or settled feature in the codebase.
- `codebase-design`: codebase architecture design.
- `domain-modeling`: domain concepts, invariants, aggregates, boundaries.
- `diagnosing-bugs`: root-cause analysis before fixing.
- `resolving-merge-conflicts`: conflict resolution and safe integration.
- `to-prd`: turn a shaped idea into a PRD.
- `to-issues`: split a PRD into implementation issues.
- `triage`: classify and shape raw issues or requests.
- `improve-codebase-architecture`: identify and plan codebase health improvements.
- `prototype`: answer uncertain questions with disposable runnable code.
- `grill-with-docs`: interview against project docs/code and preserve context.
- `review`: review code or plans for quality and regressions.
- `decision-mapping`: map choices through Decision Gate discipline.
- `grilling`: relentless one-question-at-a-time plan interview.
- `grill-me`: user-invoked shortcut to `grilling`.
- `handoff`: compact current context for a new session.
- `teach`: stateful learning workspace.
- `writing-great-skills`: reference for skill design.
- `setup-pre-commit`: install pragmatic polyglot pre-commit checks.
- `git-guardrails`: git safety rules and optional local hook.
- `which-skill`: this router.

## Agent quick map

- Archimedes: architecture, modules, patterns, boundaries, tradeoffs.
- Solomon: clean code, vertical slice implementation, validators, handlers, feature isolation.
- Oracle: EF Core, SQL Server, migrations, query optimization, data performance.
- Hermes: REST APIs, DTOs, OpenAPI, status codes, versioning.
- Argus: unit/integration tests, edge cases, mocking, regression coverage.
- Sentinel: security hardening, vulnerability analysis, authorization, secrets.
- Vulcan: CI/CD, GitHub Actions, Docker, deployment, observability, rollback.
- Pixel: UX, UI flows, accessibility, frontend consistency.
- Janus: final quality gate, regressions, cross-agent consistency, production readiness.
- Scribe: engineering history, feature history, merge tracking, architecture evolution.

## Output format

When this router is used, answer with:

1. Recommended route: skill or agent sequence.
2. Why this route fits.
3. Severity: LOW, MEDIUM, or HIGH.
4. Decision Gate needed: yes/no.
5. Next concrete instruction for the user or agent.
