---
name: setup-pre-commit
description: Set up pragmatic staged-file-aware pre-commit checks for Roamly's polyglot .NET backend and React frontend using Husky, lint-staged, Prettier, dotnet format, targeted build, and targeted tests.
---

# Setup Pre-Commit Hooks

Roamly is a polyglot repository: ASP.NET Core + EF Core + SQL Server backend, React + Vite + TypeScript frontend, and GitHub Actions for full validation. The hook should be fast, staged-file-aware, and protective. Slow full test runs belong in CI, not in every commit.

## What this sets up

- Husky pre-commit hook driven from the frontend package manager.
- lint-staged for frontend formatting and lightweight checks under `frontend/**`.
- Prettier config if the repo does not already have one.
- A small PowerShell pre-commit script that scopes checks from staged files.
- Backend checks for staged C# project files: `dotnet format --verify-no-changes`, targeted `dotnet build`, and targeted `dotnet test` where affected test projects are obvious.
- Clear escape hatch guidance for rare emergency commits, without normalizing bypasses.

## Steps

### 1. Inspect repository layout

Find the frontend package root and backend solution/project roots. Common Roamly candidates:

- Frontend: `frontend\package.json`
- Backend: `backend\*.sln`, `src\*.sln`, or repository-root `*.sln`
- Tests: `tests\**\*.csproj`, `*.Tests.csproj`, frontend `vitest` config, Playwright config

Do not assume exact paths without checking the repository.

### 2. Detect the frontend package manager

In the frontend package root, check for lockfiles:

- `package-lock.json` -> npm
- `pnpm-lock.yaml` -> pnpm
- `yarn.lock` -> yarn
- `bun.lockb` or `bun.lock` -> bun

Default to npm only if unclear.

### 3. Install frontend dev dependencies

Install only in the frontend package root:

```powershell
npm install --save-dev husky lint-staged prettier
```

Adapt the command for pnpm, yarn, or bun. Do not add Node dependencies at the repository root unless that is where the frontend package actually lives.

### 4. Initialize Husky

From the frontend package root, initialize Husky using the detected package manager. For npm:

```powershell
npx husky init
```

If Husky creates `.husky` inside the frontend folder but Roamly expects repository-root hooks, move or configure the hook deliberately. Prefer repository-root `.husky\pre-commit` so backend checks run from the repo root.

### 5. Configure lint-staged

Scope lint-staged to frontend files. Example `.lintstagedrc.json` at repo root:

```json
{
  "frontend/**/*.{ts,tsx,js,jsx,json,css,scss,md}": [
    "prettier --ignore-unknown --write"
  ]
}
```

If package scripts already expose lint or typecheck commands that can accept file scoping, add them only when they remain fast. Avoid running the entire frontend test suite from lint-staged.

### 6. Add Prettier config if missing

Create `.prettierrc` only if no Prettier config exists:

```json
{
  "useTabs": false,
  "tabWidth": 2,
  "printWidth": 100,
  "singleQuote": false,
  "trailingComma": "es5",
  "semi": true,
  "arrowParens": "always"
}
```

### 7. Add a staged-file-aware PowerShell script

Create `.husky\pre-commit` that invokes a script such as `scripts\pre-commit.ps1` from the repo root:

```sh
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/pre-commit.ps1
```

The PowerShell script should:

1. Read staged files with `git diff --cached --name-only --diff-filter=ACMR`.
2. Run lint-staged only when staged files include `frontend\` paths supported by the lint-staged config.
3. Run `dotnet format --verify-no-changes --no-restore` when staged files include `.cs`, `.csproj`, `.props`, `.targets`, `.sln`, or `.editorconfig`.
4. Run targeted `dotnet build` for affected backend solution/project roots. If scoping is ambiguous, build the main solution once.
5. Run targeted `dotnet test --no-build` for directly affected test projects. If only production backend files changed and mapping is unclear, skip local tests and rely on CI unless the changed area has an obvious adjacent test project.
6. Print exactly what was checked and what was intentionally left to CI.

Keep the hook pragmatic. A pre-commit hook that takes minutes will be bypassed; GitHub Actions should run full backend tests, frontend tests, Playwright, security checks, and container validation.

### 8. Verification

- `.husky\pre-commit` exists and calls PowerShell on Windows.
- lint-staged runs successfully for a staged frontend file.
- `dotnet format --verify-no-changes --no-restore` runs successfully when a C# file is staged.
- A targeted `dotnet build` succeeds for an affected backend project or the main solution.
- Hook output documents skipped slow checks and points to CI for full validation.

### 9. Commit guidance

Stage the hook/config changes and commit with a message such as:

```text
Add polyglot pre-commit guardrails
```

Do not commit generated build output, secrets, user-specific IDE files, or package manager artifacts unrelated to the chosen package manager.
