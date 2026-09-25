---
name: diagnosing-bugs
description: Diagnosis loop for hard Roamly bugs and performance regressions. Use when the user says diagnose or debug this, reports broken behavior, exceptions, failing tests, flaky behavior, or slow API/UI flows.
---

# Diagnosing Bugs

A discipline for hard bugs. Skip phases only when explicitly justified.

When exploring Roamly, read `docs/architecture/CONTEXT.md` and relevant ADRs to understand module vocabulary and prior decisions. Bugs often cross a vertical slice boundary: API contract, validator, handler, EF Core query, React route, TanStack Query cache, or background workflow.

## Phase 1 - Build a feedback loop

**This is the skill.** Everything else consumes the loop. If you have a tight pass/fail signal for the bug - one that goes red on this exact bug - you can find the cause. If you do not, staring at code will not save you.

Spend disproportionate effort here. Be aggressive, creative, and unwilling to proceed without a red-capable signal.

### Ways to construct one

Try these in roughly this order:

1. **Failing test** at the seam that reaches the bug: xUnit unit/integration test, Vitest test, or Playwright test.
2. **HTTP script** against the ASP.NET Core dev server using `Invoke-RestMethod`, `curl.exe`, or a checked-in API test helper.
3. **Command or worker invocation** with a fixture input and expected output.
4. **Headless browser script** with Playwright: drive the UI and assert DOM, console, or network behavior.
5. **Replay a captured trace**: HAR file, API payload, event log, or saved JSON request through the code path.
6. **Throwaway harness**: run a minimal subset of the system that exercises the failing handler, EF Core query, or React component.
7. **Property or fuzz loop**: for sometimes-wrong calculations such as expense totals, date ranges, or checklist state.
8. **Bisection harness**: automate the bug check for `git bisect run` when the regression appeared between known commits.
9. **Differential loop**: run the same input through old vs new code, or two configurations, and diff outputs.
10. **Human-in-the-loop PowerShell script**: last resort. If a human must click, copy and edit `scripts/hitl-loop.template.ps1` so the loop is still structured.

Build the right feedback loop and the bug is mostly fixed.

### Tighten the loop

Treat the loop as a product:

- Can it be faster? Cache setup, skip unrelated app initialization, narrow the test scope.
- Can the signal be sharper? Assert the specific symptom, not merely "did not crash".
- Can it be more deterministic? Pin time, seed randomness, isolate filesystem, freeze network, use stable test data.
- Can it run unattended? Prefer scripts/tests over manual steps.

A 30-second flaky loop is barely better than no loop; a 2-second deterministic loop is a debugging superpower.

### Non-deterministic bugs

The goal is not always a perfect repro; it is a higher reproduction rate. Loop the trigger many times, run in parallel, add stress, narrow timing windows, and inject sleeps only to increase signal. A 50 percent flake is debuggable; a 1 percent flake usually is not. Keep raising the rate until it is usable.

### When you genuinely cannot build a loop

Stop and say so explicitly. List what you tried. Ask for one of:

- access to the environment that reproduces it,
- a captured artifact such as HAR file, log dump, database snapshot, trace, or screen recording with timestamps,
- permission to add temporary instrumentation.

Do not proceed to confident hypotheses without a loop.

### Completion criterion: a tight loop that can go red

Phase 1 is done when you can name one command that you have already run at least once and that is:

- [ ] **Red-capable**: drives the actual bug code path and asserts the user's exact symptom.
- [ ] **Deterministic**: same verdict every run, or high reproduction rate for flaky bugs.
- [ ] **Fast**: seconds where possible, not minutes.
- [ ] **Agent-runnable**: unattended unless using the HITL script.

If you catch yourself building a theory before this command exists, stop. No red-capable command, no Phase 2.

## Phase 2 - Reproduce and minimize

Run the loop. Watch it go red.

Confirm:

- [ ] The failure mode matches the user's symptom, not a nearby issue.
- [ ] The failure is reproducible enough to debug.
- [ ] The exact symptom is captured: error message, status code, wrong response, bad UI state, slow timing, or incorrect SQL behavior.

### Minimize

Shrink the repro to the smallest scenario that still goes red. Cut inputs, callers, config, data, and steps one at a time, rerunning the loop after each cut.

Examples:

- Reduce a Trip with six stops to the two stops that trigger ordering failure.
- Remove unrelated camper documents from the fixture.
- Replace a full browser journey with one API call if the UI only exposes the backend bug.
- Replace a full SQL dataset with the minimal rows that produce the wrong EF Core query result.

Done when every remaining element is load-bearing. Removing any one of them makes the loop go green.

## Phase 3 - Hypothesize

Generate **3-5 ranked hypotheses** before testing any of them. Single-hypothesis debugging anchors on the first plausible idea.

Each hypothesis must be falsifiable and state its prediction.

Format:

> If <X> is the cause, then <changing or observing Y> will make the bug disappear, move, or become worse.

If you cannot state the prediction, the hypothesis is a vibe. Discard or sharpen it.

Show the ranked list to the user when interactive work is possible. If the user is unavailable, proceed with the ranking but record it in the work notes or PR summary.

## Phase 4 - Instrument

Each probe must map to a specific prediction from Phase 3. Change one variable at a time.

Tool preference:

1. Debugger, REPL, or focused inspection when available.
2. Targeted logs at boundaries that distinguish hypotheses.
3. Database query plan or EF Core SQL output for data bugs.
4. Browser console/network tracing for UI bugs.

Never "log everything and grep".

Tag every temporary debug log with a unique prefix such as `[DEBUG-a4f2]`. Cleanup then becomes a single search. Untagged logs survive; tagged logs must be removed.

### Performance branch

For performance regressions, logs are usually the wrong first tool. Establish a baseline measurement first:

- API duration with a repeatable request.
- EF Core generated SQL and execution plan.
- Browser performance trace or React render count.
- Query count to catch N+1 behavior.

Measure first, fix second.

## Phase 5 - Fix and regression test

Write the regression test **before the fix**, but only if there is a correct seam.

A correct seam exercises the real bug pattern as it occurs at the call site. If the bug needs multiple callers or real EF Core translation, a tiny unit test may give false confidence.

If no correct seam exists, that is a finding. The architecture is preventing the bug from being locked down. Document it and consider a follow-up design task.

If a correct seam exists:

1. Turn the minimized repro into a failing test at that seam.
2. Watch it fail for the right reason.
3. Apply the fix.
4. Watch the test pass.
5. Re-run the original un-minimized Phase 1 loop.

## Phase 6 - Cleanup and post-mortem

Required before declaring done:

- [ ] Original repro no longer reproduces.
- [ ] Regression test passes, or absence of a correct seam is documented.
- [ ] All `[DEBUG-...]` instrumentation removed.
- [ ] Throwaway harnesses deleted or moved to a clearly marked debug location outside production paths.
- [ ] The correct hypothesis is stated in the PR/commit summary so the next debugger learns.

Then ask: what would have prevented this bug? If the answer involves architecture - no good test seam, tangled callers, hidden coupling, or unclear ownership - raise a follow-up with the codebase-design or domain-modeling skill after the fix is in.

## Roamly agent alignment

Bug diagnosis is jointly owned by **Solomon** and **Argus**. Data/performance bugs involve **Oracle**; API contract bugs involve **Hermes**; security-sensitive bugs involve **Sentinel**; production readiness goes through **Janus**.

Any MEDIUM or HIGH decision discovered during debugging requires the orchestrator's **Decision Required** consult format. Examples: changing database indexes, altering authorization behavior, replacing a library, changing slice boundaries, adding infrastructure, or accepting a compatibility break. LOW fixes that preserve behavior and only repair the bug can proceed directly.