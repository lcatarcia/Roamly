# Out-of-Scope Knowledge Base

Roamly may keep a repo-local out-of-scope knowledge base for rejected enhancement requests. It exists for institutional memory and deduplication during GitHub Issue triage.

Use it only for enhancements rejected as `wontfix`. Do not use it for bugs, already-implemented requests, temporary deferrals, or work that is merely lower priority.

## Purpose

1. **Institutional memory** - preserve why a feature was rejected so the reasoning is not lost when the issue is closed.
2. **Deduplication** - when a new GitHub Issue repeats a rejected idea, surface the previous decision instead of re-litigating it from scratch.

## Directory structure

Use one Markdown file per concept:

```text
.out-of-scope\dark-mode.md
.out-of-scope\public-trip-marketplace.md
.out-of-scope\offline-first-sync.md
```

One file per concept, not per issue. Multiple issues requesting the same thing are grouped under the same file.

## File format

Write a short durable design note, not a database entry. Explain the decision in Roamly terms and reference architecture constraints when useful.

```markdown
# Public Trip Marketplace

Roamly does not currently support a public marketplace where users publish trips for discovery by other users.

## Why this is out of scope

Roamly is scoped as a private camper and travel management app. A public marketplace would introduce moderation, public profiles, search ranking, abuse handling, privacy controls, and a different authorization model.

That conflicts with the current modular monolith assumptions around user-owned Campers, Trips, Places, Expenses, Checklists, Documents, and JournalEntries. It would also require security and compliance decisions beyond the current product direction.

## Prior requests

- #42 - "Share my trips publicly"
- #87 - "Discover routes from other campers"
```

### Naming the file

Use a short descriptive kebab-case concept name:

- `public-trip-marketplace.md`
- `offline-first-sync.md`
- `multi-tenant-fleet-management.md`

The name should be understandable while browsing the directory.

### Writing the reason

The reason must be substantive. Good reasons reference:

- Product scope: Roamly focuses on private camper/travel management rather than public social discovery.
- Architecture: the request would conflict with current module boundaries, authorization, or data ownership.
- Operational cost: the feature requires moderation, infrastructure, compliance, or support capacity outside the project goals.
- Strategy: an ADR or product decision intentionally chose a different direction.

Avoid temporary reasons such as "not enough time". Those are deferrals, not durable rejections.

## When to check out-of-scope records

During triage context gathering, read `.out-of-scope\*.md` when the directory exists. Match by concept, not exact wording.

Examples:

- "night mode" matches `dark-mode.md`.
- "share trip plans publicly" matches `public-trip-marketplace.md`.
- "use the app without internet and sync later" matches `offline-first-sync.md`.

If there is a match, surface it to the maintainer:

> This resembles `.out-of-scope\public-trip-marketplace.md`. We rejected it before because Roamly is scoped around private trip management and this would require public moderation and a different authorization model. Do you still want to keep that decision?

The maintainer may:

- **Confirm** - add the new issue to the existing file's Prior requests and close it with `wontfix`.
- **Reconsider** - delete or update the out-of-scope file and continue normal triage.
- **Distinguish** - explain why the new issue is different; continue normal triage.

## When to write to the knowledge base

Only when an enhancement is rejected as `wontfix`.

Do not write when:

- The requested behavior already exists. Close with a comment pointing to the implementation instead.
- The item is a bug.
- The item is deferred, not rejected.
- The decision requires a full ADR instead of a triage note.

Flow:

1. Maintainer decides the enhancement is out of scope.
2. Check for an existing matching `.out-of-scope` file.
3. If one exists, append the new issue to Prior requests.
4. If none exists, create a file with the concept, decision, reason, and first prior request.
5. Comment on the GitHub Issue, starting with the triage disclaimer, explaining the decision and linking the file.
6. Close the issue with `wontfix` and keep the category label `enhancement`.

## Updating or removing records

If the maintainer changes direction:

- Delete the out-of-scope file or update it to describe the new decision.
- Do not reopen old issues automatically; they remain historical records.
- The new issue that triggered reconsideration continues through normal triage.

If the reason is architecture-significant, offer to record or update an ADR so future agents and Janus can evaluate it consistently.
