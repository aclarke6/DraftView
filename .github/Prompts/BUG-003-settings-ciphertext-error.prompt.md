---
mode: agent
description: BUG-003 — Reader settings shows Ciphertext decryption error as form validation message
---

# BUG-003 — Reader settings shows `Ciphertext is not in the expected format`

## Branching
1. Checkout `BugFix-PC` (or `BugFix-Mac` if on Mac) and pull latest from `main`
2. Create and checkout `bugfix/BUG-003-settings-ciphertext-error` from `BugFix-PC`
3. All work is done on `bugfix/BUG-003-settings-ciphertext-error`
4. When all Success Gates pass, present the merge commands to the developer — do not execute them
5. Developer merges: `bugfix/BUG-003-...` → `BugFix-PC` → `main`

## Symptoms
1. Reader navigates to `Account/Settings`
2. Reader submits the settings form (display name or password change)
3. The page returns with a form validation error: `Ciphertext is not in the expected format`
4. This error is displayed as a user-facing form validation message
5. Expected: operational failures of this kind must never surface as form validation errors — they must be logged and routed to the controlled 500 error path

## Where to Start Looking

Read the following files in the order given. Do not write any code yet.

1. `DraftView.Web/Controllers/AccountController.cs` — all settings POST actions
   - Read each action that handles a form submission from Account Settings
   - Ask: which action triggers a decryption attempt?
   - Ask: where is the exception caught, and how does it end up in `ModelState`?

2. `DraftView.Application/Services/UserService.cs` — any method called from the settings actions
   - Ask: does any method here call email decryption directly or indirectly?
   - Ask: could an `InvalidOperationException` or `FormatException` from decryption propagate to the controller?

3. `DraftView.Infrastructure/Persistence/Repositories/UserRepository.cs` — `HydrateEmail` and related methods
   - Ask: when is email decryption attempted?
   - Ask: what exception is thrown when `EmailCiphertext` is invalid?

4. `DraftView.Domain/Entities/User.cs`
   - Read `LoadEmailForRuntime` and `SetProtectedEmail`
   - Ask: is there any guard against invalid ciphertext values?

## What to Produce — Plan First, Then Pause

After reading all files, produce a written plan containing **all four sections** below.
**Stop after the plan. Do not write any code. Wait for the plan to be reviewed and approved by the developer.**

### Section 1 — Root Cause Analysis
State precisely:
- Which action triggers the decryption attempt
- Where the exception originates (repository, service, or controller)
- How it ends up displayed as a form validation error rather than routing to the 500 path
- Whether this is a code path error, bad data in the database, or both
- Whether any production rows have malformed `EmailCiphertext` values (check with `/tmp/run-query.sh`)

### Section 2 — Failing Test Plan
List each test that must be written. For each test state:
- The test class and method name
- What it seeds / arranges
- What it calls
- What assertion it makes
- Why that assertion currently fails (red)
- Why it will pass after the fix (green)

Tests must cover:
- That a decryption failure in a settings action routes to the controlled error path, not `ModelState`
- That the error does not surface as a user-facing validation message

### Section 3 — Proposed Fix
Describe the fix in plain English before touching any code. State:
- Which file(s) change and why
- What the change is — specifically how the exception is caught and rerouted
- What the change is NOT (not a null guard, not a workaround for bad database rows)
- Whether a migration is required

### Section 4 — Success Gates

**Gate 1 — New tests are red before the fix**
- [ ] All new tests confirmed red — paste failing output

**Gate 2 — New tests are green after the fix**
- [ ] All new tests pass — paste passing output

**Gate 3 — No regressions**
- [ ] Full suite run — paste count: X passing, 0 failed, 1 skipped

**Gate 4 — Browser verification**
- [ ] Reproduce the error in the browser before the fix (confirm symptom)
- [ ] Confirm the error no longer surfaces as a form validation message after the fix
- [ ] Confirm the 500 error path is reached for decryption failures

**Gate 5 — Committed to GitHub**
- [ ] Committed to `bugfix/BUG-003-settings-ciphertext-error` with message:
      `bugfix: BUG-003 — route ciphertext decryption failures to controlled error path`
- [ ] `git status` is clean

**Gate 6 — TASKS.md updated**
- [ ] `TASKS.md` updated to mark BUG-003 as `[DONE]` with date and resolution summary
- [ ] Included in same commit batch

**Gate 7 — Present merge commands**
- [ ] Present for manual execution — do not execute:
  ```
  git checkout BugFix-PC
  git merge bugfix/BUG-003-settings-ciphertext-error
  git checkout main
  git merge BugFix-PC
  git push
  ```
  Then run `publish-draftview.ps1` to deploy to production.

## Rules
- Do not change any production code until the plan has been reviewed and approved by the developer
- TDD: failing test → confirm red → fix → confirm green
- Operational failures must route to the controlled error path — never leak as form validation messages
- Do not fix bad database rows as a workaround — fix the code path
- All git commands presented to developer for manual execution — never executed automatically
- A task is not complete until every Success Gate is confirmed
