# RESOURCES.md Format

`RESOURCES.md` is the curated set of trusted sources for this topic. Knowledge for lessons should be drawn from here, project docs, or source code, not from guesses. Wisdom comes from the communities and review paths listed here.

## Structure

```md
# {Topic} Resources

## Knowledge

- [Microsoft Learn: ASP.NET Core documentation](https://learn.microsoft.com/aspnet/core/)
  Official platform reference. Use for: middleware, configuration, hosting, APIs, and security fundamentals.
- [Roamly architecture docs](docs/architecture)
  Project-specific architecture decisions. Use for: module boundaries, vertical slice conventions, and tradeoffs already accepted.

## Wisdom (Communities and Review Paths)

- Roamly Janus final review
  Use for: production-readiness judgement, regression risks, and cross-agent consistency.
- GitHub issue or PR discussion
  Use for: project-specific design feedback and durable decision history.
```

## Rules

- High-trust only. Prefer official docs, primary sources, recognised experts, ADRs, source code, and moderated communities.
- Annotate every entry. A bare link is useless later. Add what it covers and when to use it.
- Group by Knowledge and Wisdom. It is fine for a resource to appear in only one group.
- Surface gaps explicitly with a `## Gaps` section when the mission needs something you have not found.
- Prune ruthlessly. Better five sharp sources than thirty mediocre ones.
- Record community preferences. If the user opts out of communities, note it so future sessions do not keep proposing them.
