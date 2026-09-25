---
name: git-guardrails
description: Define Roamly git safety guardrails and optionally install a local Windows pre-push hook that blocks dangerous history, cleanup, and secret-related operations.
---

# Git Guardrails

This skill is guidance-first. Copilot CLI does not need Claude-specific tool hooks to stay safe: it needs explicit rules, human-in-the-loop escalation, and optional local Git hooks for push-time protection.

## Guardrail rules

Treat these commands as forbidden unless the user explicitly authorizes the exact command and target after seeing the risk.

### Always forbidden for autonomous execution

- `git push --force` or `git push --force-with-lease`: can overwrite remote work and break other sessions.
- `git reset --hard`: destroys local changes that may include user work.
- `git clean -fd`, `git clean -fdx`, or equivalent recursive cleanup: deletes untracked files, including user notes or generated artifacts not yet committed.
- `git checkout -- <path>`, `git restore <path>`, `git restore .`, or `git checkout .` when the worktree is dirty: discards local edits without review.
- `git rebase`, `git filter-branch`, `git filter-repo`, or amend/squash operations on shared branches: rewrites history and can invalidate other agents' assumptions.
- Committing secrets, credentials, tokens, connection strings, certificates, private keys, or personal data.

### Requires a Decision Gate

Use the orchestrator's Decision Gate before operations with MEDIUM/HIGH impact:

- Any history rewrite on a branch that may have been pushed or shared.
- Any destructive cleanup of untracked files.
- Any CI/CD, deployment, authentication, authorization, database migration, or secret-management change.

Present at least four options, mark one RECOMMENDED, and wait for the user.

## Safer alternatives

- Before discarding changes, show `git status --short` and `git diff -- <path>`.
- Prefer moving unwanted files aside or asking the user to delete them.
- Prefer `git revert` over history rewrite for pushed commits.
- Prefer a new branch over force-pushing a shared branch.
- Use secret scanners and `.gitignore` updates before committing suspicious files.

## Optional local pre-push hook

This skill includes a Windows PowerShell helper at [`scripts/block-dangerous-git.ps1`](scripts/block-dangerous-git.ps1). It is intentionally local and generic: it can be copied into `.git\hooks\pre-push` or called from a custom wrapper, but it is not a replacement for judgement.

Example `.git\hooks\pre-push` on Windows:

```powershell
#!/usr/bin/env pwsh
pwsh -NoProfile -ExecutionPolicy Bypass -File .github/skills/git-guardrails/scripts/block-dangerous-git.ps1 -Mode PrePush
```

If Git for Windows does not execute PowerShell hooks directly in the environment, create `.git\hooks\pre-push` as a small `sh` shim that calls `pwsh` with the same script path.

## Verification

Run a dry command check without touching Git state:

```powershell
.github\skills\git-guardrails\scripts\block-dangerous-git.ps1 -Command "git push --force-with-lease origin main"
```

Expected result: exit code 2 and a `BLOCKED` message explaining the matched dangerous pattern.
