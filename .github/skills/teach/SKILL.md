---
name: teach
description: User-invoked stateful teaching workspace skill for learning Roamly concepts, technologies, architecture, testing, or engineering practices over multiple sessions.
---

# Teach

> User-invoked skill: run it only when the user explicitly asks for it.

The user has asked you to teach them something. This is a stateful request: they intend to learn the topic over multiple sessions.

## Teaching workspace

Treat the current directory as the teaching workspace unless the user names another location. In Roamly, prefer durable learning artifacts that can coexist with the product repository without polluting production docs. Do not modify product docs unless the user explicitly asks.

Learning state is captured in these files:

- `MISSION.md`: the reason the user is learning this topic. Use it to ground all teaching. Format: [MISSION-FORMAT.md](./MISSION-FORMAT.md).
- `./reference/*.html`: compressed reference materials from lessons: cheat sheets, diagrams, command maps, syntax references, architecture maps, domain vocabulary, or testing patterns.
- `RESOURCES.md`: trusted sources to ground teaching. Format: [RESOURCES-FORMAT.md](./RESOURCES-FORMAT.md).
- `./learning-records/*.md`: learning records that capture demonstrated understanding, prior knowledge, misconceptions corrected, and mission shifts. Format: [LEARNING-RECORD-FORMAT.md](./LEARNING-RECORD-FORMAT.md).
- `./lessons/*.html`: self-contained lessons. A lesson teaches one tightly-scoped thing tied to the mission.
- `./assets/*`: reusable components shared across lessons: stylesheets, quiz widgets, diagrams, simulators, examples.
- `NOTES.md`: scratchpad for user preferences and working notes.
- `GLOSSARY.md`: canonical terms once the learner has demonstrated understanding. Format: [GLOSSARY-FORMAT.md](./GLOSSARY-FORMAT.md).

## Roamly anchors

When the topic touches Roamly engineering, ground examples in the actual stack:

- Backend: C#, ASP.NET Core 10, EF Core, SQL Server, REST/OpenAPI, Vertical Slice Architecture, CQRS, FluentValidation, xUnit, modular monolith.
- Modules: Identity, Campers, Maintenance, Documents, Trips, Places, Expenses, Checklists, Journal, Intelligence.
- Frontend: React, Vite, TypeScript, React Router, TanStack Query, Vitest/Testing Library, Playwright.
- DevOps: GitHub, GitHub Issues, GitHub Actions, Docker.
- Docs: `docs/architecture`, `docs/product`, `docs/adr`.
- Agents: Archimedes, Solomon, Oracle, Hermes, Argus, Sentinel, Vulcan, Pixel, Janus, Scribe.

If a lesson involves production choices, respect the orchestrator: Decision Gate for multiple valid strategies, severity LOW/MEDIUM/HIGH, human-in-the-loop, and Janus as final production-readiness gate.

## Philosophy

Deep learning needs three things:

- Knowledge, captured from high-quality, high-trust resources.
- Skills, acquired through relevant practice and feedback loops.
- Wisdom, developed by applying skills in real contexts and learning from practitioners.

Before `RESOURCES.md` is well-populated, focus on finding high-quality resources. Do not rely on parametric memory for technical claims that matter. For Roamly topics, project docs and source code are first-class resources.

Some topics are knowledge-heavy, such as EF Core query behaviour or authorization concepts. Others are skill-heavy, such as writing vertical slices, designing tests, or debugging a flaky Playwright scenario.

### Fluency vs storage strength

Separate two kinds of learning:

- Fluency strength: in-the-moment retrieval of knowledge.
- Storage strength: long-term retention of knowledge.

Fluency can create an illusion of mastery. Storage strength is the real goal. Design lessons with desirable difficulty:

- Retrieval practice: ask the learner to recall before revealing.
- Spacing: revisit concepts after time has passed.
- Interleaving: mix related concepts in practice when it helps skill transfer.

## Lessons

A lesson is the main unit you produce: one self-contained HTML file saved to `./lessons/`, titled `0001-dash-case-name.html`, incrementing each time.

A lesson should be beautiful, readable, and printable. Think clean typography, generous whitespace, useful diagrams, and minimal distraction.

A lesson should be short and quickly completable. Working memory is limited. Each lesson should give the user one tangible win tied to the mission and the learner's zone of proximal development.

If possible, open the lesson file for the user with an appropriate CLI command. On Windows, that may be `Start-Process .\lessons\0001-name.html`.

Each lesson should:

- Link to other lessons and reference documents with HTML anchors.
- Recommend a primary source to read or watch next.
- Cite sources for factual claims.
- Include a reminder that the user can ask follow-up questions.
- Include retrieval practice, not just exposition.
- Avoid overloading the learner with Roamly details that are not needed for the current skill.

## Assets

Lessons are built from reusable components in `./assets/`: stylesheets, quiz widgets, diagram helpers, small simulators, or code display components.

Reuse is the default. Before writing a lesson, inspect `./assets/` and build from what exists. When a lesson needs something reusable, write it as a component in `./assets/` and link to it rather than duplicating inline code.

A shared stylesheet is the first component every workspace earns. Every lesson should link it so the course feels coherent.

## The mission

Every lesson must trace to `MISSION.md`: the reason the user is learning.

If the user is unclear about the mission, or `MISSION.md` does not exist, your first job is to interview them on why they want to learn this. A vague mission leads to abstract lessons and poor sequencing.

Missions can change as the user learns. When the goal changes, update `MISSION.md` and add a learning record explaining why. Confirm the change with the user when interactive; in non-interactive contexts, preserve the old mission and write a proposed update instead.

## Zone of proximal development

Each lesson should challenge the user just enough.

If the user names an exact thing they want to learn, teach that if it fits the mission. If they do not, infer the next lesson by reading:

- `MISSION.md`
- `learning-records`
- `GLOSSARY.md`
- `NOTES.md`
- relevant Roamly docs/source when the topic is project-specific

Then teach the most relevant thing that fits their current floor.

## Knowledge

Lessons should be designed around a skill the user will acquire. Teach only the knowledge required to acquire that skill, then move into practice.

Gather knowledge from trusted resources first. Use `RESOURCES.md` to track them. For Roamly topics, valid resources include official Microsoft docs, React/TanStack docs, GitHub Actions docs, the Roamly docs tree, ADRs, and source files.

Knowledge acquisition should reduce difficulty. Difficulty consumes working memory needed for understanding. Save desirable difficulty for practice.

## Skills

Skill acquisition is about durability and flexibility. Make knowledge stick.

Use interactive lessons and tight feedback loops:

- Quizzes with immediate feedback.
- Small code-reading exercises.
- Step-by-step debugging tasks.
- Design classification tasks: which agent, which skill, which severity?
- Refactoring judgement exercises.
- Test-first micro-katas for backend or frontend code.

For quizzes, make answer choices roughly equal in length so formatting does not reveal the answer.

## Acquiring wisdom

Wisdom comes from real-world interaction. When a question requires judgement from practice, answer as far as you can, then point to a community, project maintainer, or Roamly agent/human review path.

For Roamly engineering wisdom, the community may be internal project history: ADRs, PR discussions, issues, Scribe records, and Janus reviews. If external community input would help, prefer high-reputation, moderated sources.

Respect the user's preference if they do not want community participation.

## Reference documents

Create reference documents alongside lessons when a compressed artifact will be useful later. Lessons may not be revisited; reference documents will be.

Good Roamly references include:

- Vertical slice checklist.
- EF Core query and migration pitfalls.
- FluentValidation patterns.
- React Query mutation lifecycle.
- Testing pyramid for Roamly.
- Agent selection map.
- Glossary of domain terms.

Glossaries are especially important. Once `GLOSSARY.md` exists, adhere to it in every lesson.

## NOTES.md

Use `NOTES.md` for teaching preferences, pacing, constraints, and working notes. Keep it concise. Do not turn it into a session transcript.
