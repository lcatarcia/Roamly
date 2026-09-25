---
name: writing-great-skills
description: User-invoked reference for writing and editing project-level Copilot CLI skills with predictable invocation, structure, routing, and maintenance.
---

# Writing Great Skills

> User-invoked skill: run it only when the user explicitly asks for it.

A skill exists to wrangle determinism out of a stochastic system. Predictability - the agent taking the same process every run, not producing the same output - is the root virtue. Every lever below serves it.

Bold terms are defined in [`GLOSSARY.md`](GLOSSARY.md). Look them up there for the full meaning.

## Invocation

Project-level Copilot CLI skills use frontmatter with exactly `name` and `description`. The practical invocation choice is still the same:

- A **model-invoked** skill keeps a trigger-rich **description**, so the agent can select it autonomously and other skills can point to it. It contributes to **context load** because the description is visible as a trigger surface.
- A **user-invoked** skill is marked in the body, immediately under the H1, with: `> User-invoked skill: run it only when the user explicitly asks for it.` It still needs a description for the skill registry, but the body must say the agent should not run it autonomously. It spends **cognitive load**: the human is the index that remembers it exists.

Pick model invocation only when the agent must reach the skill on its own, or another skill must route to it. If it only fires by hand, make it user-invoked and rely on a router such as `which-skill`.

When user-invoked skills multiply past what a person can remember, cure the cognitive load with a **router skill**: one user-invoked skill that names the others and when to reach each.

## Writing the description

A model-invoked **description** does two jobs: state what the skill is, and list the **branches** that should trigger it. Every word increases context load, so descriptions must be pruned harder than bodies.

- Front-load the skill's **leading word**.
- Include one trigger per branch. Synonyms that rename a single branch are **duplication**.
- Cut identity already present in the body.
- Include Roamly-specific trigger language when it matters: vertical slice, EF Core, React Query, ADR, PRD, GitHub Issues, CI, Decision Gate, Janus, and agent names.
- Quote the description if it contains a colon.

## Information hierarchy

A skill is built from **steps** and **reference**. The core decision is which to use and where each sits on the **information hierarchy**, ranked by how immediately the agent needs the material:

1. **In-skill step**: ordered action in `SKILL.md`, the primary tier. Each step ends on a **completion criterion** that tells the agent the work is done. Make it checkable and exhaustive where it matters.
2. **In-skill reference**: a definition, rule, or fact in `SKILL.md`, consulted on demand.
3. **Disclosed reference**: material pushed into a sibling file, reached by a **context pointer**. Examples: `GLOSSARY.md`, `MISSION-FORMAT.md`, checklists, or templates.
4. **External reference**: project docs, ADRs, source files, orchestrator docs, or GitHub issues referenced by path or URL.

A demanding completion criterion drives thorough **legwork** whether the skill has steps or not. "Apply every rule" binds flat reference just as "complete every step" binds a sequence.

Push too little down and the top bloats. Push too much down and you hide material the agent actually needs. That tension is the whole decision.

**Progressive disclosure** moves reference down the ladder so `SKILL.md` stays legible. Mechanics: link a sibling `.md` file in the skill folder, named for what it holds. Branching is the cleanest disclosure test: inline what every branch needs, and disclose what only some branches reach. A **context pointer**'s wording, not its target, decides when the agent reaches the material.

Where the ladder decides how far down a piece sits, **co-location** decides what sits beside it once there: keep a concept's definition, rules, and caveats under one heading rather than scattered.

## When to split

**Granularity** is how finely you divide skills. Each cut spends one of two loads, so split only when the cut earns it.

- **By invocation**: split off a model-invoked skill when you have a distinct leading word that should trigger on its own, or another skill must reach it. You pay context load for the new description.
- **By sequence**: split a run of steps when future steps tempt the agent to rush the current step. Hiding post-completion steps can reduce **premature completion**.
- **By project role**: in Roamly, do not turn every agent into a skill. Skills define repeatable workflows; agents embody specialist judgement. `which-skill` should know when the right answer is Archimedes, Solomon, Oracle, Hermes, Argus, Sentinel, Vulcan, Pixel, Janus, or Scribe instead.

## Pruning

Keep each meaning in a **single source of truth**. One behaviour means one authoritative location.

Check every line for **relevance**: does it still bear on what the skill does?

Then hunt **no-ops** sentence by sentence. If a sentence changes nothing versus the default agent behaviour, delete it or replace it with a stronger leading word. Be aggressive. Most prose that fails should go, not be rewritten.

## Leading words

A **leading word** is a compact concept already living in the model's pretraining that the agent thinks with while running the skill: lesson, Decision Gate, vertical slice, red-green-refactor, tracer bullet, final gate, handoff, router.

It serves predictability twice:

- In the body it anchors execution: the agent reaches for the same behaviour whenever the word appears.
- In the description it anchors invocation: when the same word lives in prompts, docs, and code, the agent links that language to the skill.

Hunt for passages that can collapse into a leading word. A triad repeated in three places, or a description spending a sentence on one idea, is begging for collapse.

Examples:

- "stop and present options before choosing among several valid strategies" -> Decision Gate.
- "complete backend feature organized around one request and outcome" -> vertical slice.
- "final production-readiness review" -> Janus gate.
- "short transfer artifact for a new context window" -> handoff.

## Roamly adaptation rules

When porting or writing skills for Roamly:

- Frontmatter has exactly `name` and `description`.
- `name` equals the skill folder name.
- Body language is English.
- Use plain ASCII punctuation where possible.
- Strip personal branding, tool-specific mechanisms that do not apply, and references to another author's repository.
- Keep Roamly's stack concrete: ASP.NET Core, EF Core, SQL Server, Vertical Slice Architecture, CQRS, FluentValidation, xUnit, React, Vite, TypeScript, TanStack Query, Vitest, Playwright, GitHub Actions, Docker.
- Point project decisions to `docs/architecture`, `docs/product`, and `docs/adr` when relevant.
- Respect the orchestrator: Decision Gate for multiple valid strategies, severity LOW/MEDIUM/HIGH, human-in-the-loop, and Janus as final production-readiness gate.
- Use `which-skill` as the router over skills and agents.

## Failure modes

Use these to diagnose issues in a skill.

- **Premature completion**: ending a step before it is genuinely done. First sharpen the completion criterion; only split the sequence if the criterion is irreducibly fuzzy and the rush is observed.
- **Duplication**: the same meaning in more than one place. Costs maintenance, tokens, and predictability.
- **Sediment**: stale layers that settled because adding felt safer than removing.
- **Sprawl**: a skill too long even when every line is live. Cure it with the information hierarchy, disclosed reference, or branch splits.
- **No-op**: a line the model already obeys by default. The fix is deletion or a stronger leading word, not more prose.
