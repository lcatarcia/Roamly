# Glossary - Building Great Skills

The domain model for what makes a project-level Copilot CLI skill predictable. A skill exists to wrangle determinism out of a stochastic system; every term below is a lever on that goal. This is disclosed reference for [`writing-great-skills`](SKILL.md).

Bold terms in any definition are themselves defined in this glossary.

## Language

### Predictability

The degree to which a skill makes the agent behave the same way on every run: the same process, not the same output. A brainstorming skill should predictably diverge; its tokens vary, its behaviour does not. Predictability is the root virtue every other term serves.

_Avoid_: consistency, reliability, output determinism

### Model-Invoked

A skill whose **description** is trigger-rich enough for the agent to select autonomously. It is still reachable by the user, but it pays permanent **context load** because its description is always part of the discovery surface.

_Avoid_: automatic command, model-only skill

### User-Invoked

A skill that should run only when the user explicitly asks for it. In Roamly Copilot CLI skills, this is represented by a body line directly under the H1, not by unsupported frontmatter. It trades agent discoverability for deliberate human control and spends **cognitive load**.

_Avoid_: hidden command, manual-only tool

### Description

The skill's machine-readable trigger and the top-level **context pointer**. It states what the skill is and the branches that should invoke it. In Roamly, frontmatter must contain `name` and `description` only.

_Avoid_: summary, marketing copy

### Context Pointer

A reference held in context that names out-of-context material and encodes when to reach it. Descriptions point to skills; links inside `SKILL.md` point to disclosed reference. Wording, not the target, controls reliability.

_Avoid_: link, import

### Context Load

The cost a model-invoked skill imposes on the agent's context through its description: tokens, attention, and competition with other triggers.

_Avoid_: token cost, bloat

### Cognitive Load

The cost a user-invoked skill imposes on the human: they must know it exists and when to use it. A **router skill** reduces cognitive load.

_Avoid_: burden, human index

### Granularity

How finely skills are divided. Finer division spends context load for model-invoked skills or cognitive load for user-invoked skills. Split only when the new boundary improves invocation, sequence control, or maintainability.

_Avoid_: modularity, chunking

### Router Skill

A user-invoked skill that maps situations to skills and agents. It cannot literally execute user-invoked skills; it tells the user or agent which path fits. In Roamly, `which-skill` is the router.

_Avoid_: menu, registry

### Information Hierarchy

A skill's content ranked by how immediately the agent needs it: in-skill steps, in-skill reference, disclosed reference, external reference. The hierarchy keeps the top legible and prevents important steps from being buried.

_Avoid_: structure, outline

### Co-location

Keeping a concept's definition, rules, and caveats together so reading one part brings its neighbours with it. Co-location is the companion to the information hierarchy.

_Avoid_: grouping, clustering

### Branch

A distinct way a skill can be used. A skill with several branches needs either clearly separated sections or disclosed reference for branch-specific material.

_Avoid_: case, path

### Progressive Disclosure

Moving reference down the hierarchy and behind a context pointer so `SKILL.md` stays legible. Disclose what only some branches need; inline what every branch needs.

_Avoid_: lazy loading, hiding

### Steps

Ordered actions the agent performs. Each step needs a **completion criterion**. Not every skill has steps; some are pure reference.

_Avoid_: workflow, choreography

### Completion Criterion

The condition that tells the agent a unit of work is done. Strong criteria are checkable and, where necessary, exhaustive. They resist **premature completion** and drive **legwork**.

_Avoid_: done condition, stopping rule

### Post-Completion Steps

Steps that follow the current step. When too visible, they can pull the agent into premature completion.

_Avoid_: lookahead, horizon

### Legwork

The work the agent does inside a step: reading files, checking docs, testing, tracing, and verifying rather than asking the user to do it. Completion criteria and leading words control how much legwork happens.

_Avoid_: effort, diligence

### Reference

Material the agent consults on demand: definitions, rules, examples, formats, checklists, and caveats. It can live in `SKILL.md`, a sibling file, or external project docs.

_Avoid_: background, notes

### External Reference

Reference outside the skill folder: Roamly docs, ADRs, source files, orchestrator instructions, GitHub issues, or PRs.

_Avoid_: extra docs

### Leading Word

A compact concept already in the model's priors that anchors behaviour: Decision Gate, vertical slice, red-green-refactor, handoff, router, Janus gate. It compresses a repeated behaviour into one phrase.

_Avoid_: keyword, motif

### Single Source of Truth

The desired state where each meaning lives in exactly one authoritative place. **Duplication** violates it.

_Avoid_: canonical place, home

### Relevance

Whether a line still bears on what the skill does. A relevant line can still be a **no-op**, but irrelevant lines should be removed outright.

_Avoid_: freshness, load-bearing

## Failure modes

### Premature Completion

Ending the current step before it is genuinely done because attention slips to being finished. Defence: sharpen the completion criterion first; split the sequence only when needed.

_Avoid_: rushing, premature closure

### Duplication

The same meaning in more than one place. It costs maintenance, inflates importance, and creates disagreement when one copy changes.

_Avoid_: repetition, redundancy

### Sediment

Old content that accumulates because adding feels safe and removing feels risky. Sediment makes skills stale and hard to trust.

_Avoid_: cruft, rot

### Sprawl

A skill that is too long even if the content is live. Cure it through hierarchy, disclosed reference, branch splits, or separate skills.

_Avoid_: bloat, length

### No-Op

An instruction that changes nothing because the model already does it. The test is behavioural: does the line alter what happens? If not, delete it or replace it with a sharper leading word.

_Avoid_: restating the obvious
