# Learning Record Format

Learning records live in `./learning-records/` and use sequential numbering: `0001-slug.md`, `0002-slug.md`, and so on. Create the directory lazily, only when the first record is written.

They are the teaching equivalent of ADRs: they capture non-obvious lessons, key insights, and stated prior knowledge that will steer future sessions. They are used to calculate the learner's zone of proximal development.

## Template

```md
# {Short title of what was learned or established}

{1-3 sentences: what was learned, or what prior knowledge was established, and why it matters for future sessions.}
```

That is the whole required format. A learning record can be a single paragraph. The value is recording that this is now known and why it changes what to teach next, not filling sections.

## Optional sections

Only include these when they add genuine value:

- `Status: active | superseded by LR-NNNN` frontmatter, useful when an earlier understanding turns out to be wrong and is replaced.
- Evidence: how the user demonstrated the understanding.
- Implications: what this unlocks or rules out for future sessions.

## Numbering

Scan `./learning-records/` for the highest existing number and increment by one.

## When to write a learning record

Write one when any of these is true:

1. The user demonstrated genuine understanding of something non-trivial.
2. The user disclosed prior knowledge, including its depth.
3. A misconception was corrected.
4. The mission shifted in response to learning; cross-link to `MISSION.md` and update it.

## What does not qualify

- Material merely covered. Coverage is not learning; wait for evidence.
- Anything already captured tersely in `GLOSSARY.md` as a term definition.
- Session-by-session activity logs. Learning records are decision-grade insights, not a journal.

## Supersession

When a later record contradicts an earlier one, mark the old record `Status: superseded by LR-NNNN` rather than deleting it. The history of understanding is useful signal.
