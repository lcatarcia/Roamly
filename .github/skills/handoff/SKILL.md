---
name: handoff
description: User-invoked skill for compacting the current conversation into a safe handoff document for a fresh Copilot session or Roamly agent.
---

# Handoff

> User-invoked skill: run it only when the user explicitly asks for it.

Write a handoff document summarising the current conversation so a fresh Copilot session, project agent, or human maintainer can continue the work.

## Where to save it

Save the file under this session's persistent artifact area or another user-approved location outside source control. Do not place handoff notes into Roamly's repository unless the user explicitly asks for a committed artifact. Use UTF-8 Markdown and a filename that includes the focus and date.

## What to include

- Purpose of the handoff and the next session's intended focus, using the user's arguments if provided.
- Current state: decisions made, files changed or relevant paths, commands run, validation results, and what remains undone.
- Roamly context: affected module(s), backend/frontend/devops scope, relevant docs under `docs/architecture`, `docs/product`, or `docs/adr`, and any GitHub issue or PR references.
- Suggested skills: name the next likely skill(s), such as `which-skill`, `grilling`, `grill-with-docs`, `prototype`, `implement`, `tdd`, `review`, `resolving-merge-conflicts`, or `teach`.
- Suggested agents: name Roamly agents when appropriate: Archimedes, Solomon, Oracle, Hermes, Argus, Sentinel, Vulcan, Pixel, Janus, or Scribe.
- Decision Gate status: list any MEDIUM/HIGH decisions that still require human approval, with the recommended option if already developed.
- Risks and caveats: especially security, database, CI/CD, migration, or production-readiness concerns.

## What not to duplicate

Do not copy large content already captured in durable artifacts such as PRDs, ADRs, issues, commits, diffs, plans, or docs. Reference them by path or URL instead.

## Safety

Redact secrets, credentials, access tokens, connection strings, personal data, and any sensitive business information. If unsure whether a value is sensitive, redact it and describe the type of information removed.
