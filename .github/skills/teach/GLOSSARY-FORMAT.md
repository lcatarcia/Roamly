# GLOSSARY.md Format

`GLOSSARY.md` is the canonical language for this teaching workspace. All explainers, exercises, and learning records should adhere to its terminology. Building it is itself part of learning: compressing a concept into a tight definition is evidence the user understands it.

## Structure

```md
# {Topic} Glossary

{One or two sentence description of the topic this glossary covers.}

## Terms

**Vertical Slice**:
A feature-oriented backend unit that keeps endpoint, request/response shape, validation, handler, tests, and data access close together for one user outcome.
_Avoid_: Layered feature, mini-service

**Aggregate**:
A consistency boundary in the domain model that protects invariants through one root object.
_Avoid_: Entity cluster, table group

**Decision Gate**:
The Roamly rule that requires human choice when several valid MEDIUM or HIGH impact strategies exist.
_Avoid_: Agent preference, automatic selection
```

## Rules

- Add a term only when the user understands it. The glossary records compressed knowledge; it is not a dictionary the user reads to learn.
- Be opinionated. When several words exist for the same concept, pick the best one and list aliases to avoid.
- Keep definitions tight: one or two sentences. Define what the term is, not every way to use it.
- Use the glossary's own terms inside definitions once they exist.
- Group under subheadings when natural clusters emerge, such as Backend, Frontend, Domain, Testing, DevOps, or Agents.
- Flag ambiguities explicitly. Example: "In this workspace, slice means a Roamly vertical slice, not a UI slice."
- Revise as understanding deepens. Update in place; do not leave stale entries.
