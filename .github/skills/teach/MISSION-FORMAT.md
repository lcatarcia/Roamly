# MISSION.md Format

`MISSION.md` lives at the teaching workspace root. It captures the reason the user is learning this topic. Every teaching decision - what to teach next, which resources to surface, which exercises to design - should trace back to this document.

## Template

```md
# Mission: {Topic}

## Why
{1-3 sentences. The concrete real-world goal the user is chasing. What changes in their work or life when they have this skill? Avoid abstract framings like "understand X"; push for the underlying outcome.}

## Success looks like
- {A specific, observable thing the user will be able to do}
- {Another specific thing}
- {...}

## Constraints
- {Time, budget, prior commitments, learning preferences, project constraints, anything that bounds the approach}

## Out of scope
- {Adjacent topics the user explicitly does not want to chase right now - protects the zone of proximal development}
```

## Rules

- One mission per workspace. If the user wants to learn two unrelated things, use two workspaces.
- Concrete over abstract. "Ship a Roamly vertical slice safely" beats "learn backend architecture."
- Push back on vagueness. If the user cannot articulate why, interview them before writing anything.
- Revise when reality shifts. When the user's goal moves, update this file and record the shift.
- Keep it short. If `MISSION.md` runs past a screen, it has stopped being a compass and started being a plan.
