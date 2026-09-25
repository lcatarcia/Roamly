---
name: resolving-merge-conflicts
description: Use when you need to resolve an in-progress git merge or rebase conflict in Roamly, preserve both sides' intent, and complete validation before continuing.
---

# Resolving Merge Conflicts

1. **See the current state.** Check whether this is a merge or rebase, inspect `git status`, review the conflict list, and read the nearby commit history. Disable pagers on Windows (`git --no-pager ...`).

2. **Find the primary sources for each conflict.** Understand why each side changed. Read commit messages, linked GitHub Issues, PR descriptions, ADRs, and relevant Roamly docs. For domain terms, check `docs/architecture/CONTEXT.md`.

3. **Resolve each hunk by preserving intent.** Keep both behaviors where compatible. Where incompatible, choose the result that matches the merge/rebase goal and note the trade-off. Do not invent new behavior while resolving conflicts. Do not abort unless the user explicitly instructs it.

4. **Respect Roamly architecture.** Keep vertical slices coherent: request/response, validator, handler, endpoint/OpenAPI, EF Core query, and tests should remain consistent. Watch for conflicts that split a module term, route, migration, DTO, or frontend API client out of sync.

5. **Validate.** Discover the smallest relevant checks and run them in order: format/typecheck/build, targeted backend xUnit tests, targeted frontend Vitest/Playwright tests, then broader suites if needed. Fix issues caused by the merge.

6. **Finish the merge or rebase.** Stage resolved files and continue the operation until complete. If rebasing, repeat the process for each conflicted commit.

## Roamly agent alignment

Merge conflict resolution is owned by **Solomon**. Testing fallout goes to **Argus**, data/migration conflicts to **Oracle**, API contract conflicts to **Hermes**, security-sensitive conflicts to **Sentinel**, and final readiness to **Janus**.

If conflict resolution reveals multiple valid MEDIUM or HIGH strategies - for example competing database migration histories, API contract breaks, or module boundary changes - stop and use the orchestrator's **Decision Required** consult format with the user before choosing. Simple textual conflicts that preserve both intents are LOW severity and can be resolved directly.