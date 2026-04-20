---
mode: agent
description: BUG-006 — Unable to sync projects; sync fails with "Ciphertext is not in the expected format"
---

# BUG-006 — Unable to sync projects

## Branching
1. Checkout `BugFix-PC` (or `BugFix-Mac` if on Mac) and pull latest from `main`
2. Create and checkout `bugfix/BUG-006-unable-to-sync-projects` from `BugFix-PC`
3. All work is done on `bugfix/BUG-006-unable-to-sync-projects`
4. When all Success Gate conditions pass, present the merge commands to the developer — do not execute them
5. Developer merges: `bugfix/BUG-006-...` → `BugFix-PC` → `main`

## Symptoms
1. Author logs in and views the Dashboard
2. Both projects show sync status `Error`
3. Clicking `Sync` on either project does not recover — error persists on every attempt
4. Production service logs show the following repeating every 5 minutes:

```
fail: DraftView.Application.Services.ScrivenerSyncService
      Sync failed for project {id}: Ciphertext is not in the expected format.
      System.InvalidOperationException: Ciphertext is not in the expected format.
       ---> System.FormatException: The input is not a valid Base-64 string as it contains
            a non-base 64 character, more than two padding characters, or an illegal
            character among the padding characters.
            at DraftView.Infrastructure.Persistence.Repositories.UserRepository.GetAuthorAsync
            at DraftView.Application.Services.ScrivenerSyncService.ParseProjectAsync
```

## Where to Start Looking

Read the following files in the order given. Do not write any code yet.

1. `DraftView.Infrastructure/Persistence/DraftViewDbContext.cs`
   - Read both constructors carefully
   - Read `PrepareProtectedEmails()` and all its helper methods
   - Ask: under what circumstances does `TryCreateProtectedEmailState` return `false`?

2. `DraftView.Infrastructure/Security/UserEmailEncryptionService.cs`
   - Read both constructors carefully
   - Ask: what encryption key is in use when the parameterless constructor is called?

3. `DraftView.Infrastructure/Persistence/DraftViewDbContextFactory.cs`
   - Note which constructor it calls and what it passes

4. `DraftView.Web/Data/DatabaseSeeder.cs`
   - Trace how the Author `AppUsers` row is created on first run
   - Trace how the seeder locates an existing Author row on subsequent runs

5. `DraftView.Infrastructure/Persistence/Repositories/UserRepository.cs`
   - Read `GetAuthorAsync` and `HydrateEmail`
   - Understand what happens when `EmailCiphertext` contains a non-Base64 value

6. `DraftView.Domain/Entities/User.cs`
   - Note that `Email` is `[NotMapped]` — it is a runtime-only property
   - Understand `SetProtectedEmail` and `LoadEmailForRuntime`

## What to Produce — Plan First, Then Pause

After reading all six files, produce a written plan containing **all four sections** below.
**Stop after the plan. Do not write any code. Wait for the plan to be reviewed and approved by the developer.**

### Section 1 — Root Cause Analysis
State precisely:
- Why the `AppUsers` row contains invalid `EmailCiphertext` / `EmailLookupHmac` values
- Which code path writes those values
- Why the correct DI-keyed encryption service is not used in that path
- Why the seeder does not detect and repair the broken row on subsequent startups
- Why sync fails as a consequence

### Section 2 — Failing Test Plan
List each test that must be written. For each test state:
- The test class and method name
- What it seeds / arranges
- What it calls
- What assertion it makes
- Why that assertion currently fails (red)
- Why it will pass after the fix (green)

Tests must cover:
- The condition that produces an invalid `EmailCiphertext` on the Author row
- That `GetAuthorAsync` throws when the ciphertext is invalid
- That after the fix, a newly seeded Author row always has a valid, decryptable `EmailCiphertext`
- That the fix does not regress any existing encryption/decryption tests

### Section 3 — Proposed Fix
Describe the fix in plain English before touching any code. State:
- Which file(s) change and why
- What the change is
- What the change is NOT (confirm no null guards, no defensive workarounds)
- Whether a migration is required

### Section 4 — Success Gates
Present this checklist explicitly and confirm every item before declaring the task complete.
Do not proceed to the merge step until every gate is confirmed.

**Gate 1 — New tests are red before the fix**
- [ ] All new tests written per Section 2 are confirmed red
- [ ] Paste the failing test output showing the red count

**Gate 2 — New tests are green after the fix**
- [ ] All new tests pass after the fix
- [ ] Paste the passing test output showing the green count

**Gate 3 — No regressions**
- [ ] The full test suite has been run — not just the new tests
- [ ] Paste the full suite result: X passing, 0 failed, 0 regressions
- [ ] Any skipped tests are the same skipped tests that existed before this change

**Gate 4 — Browser verification**
- [ ] The fix has been verified manually in the browser by the developer
- [ ] Confirm: Author Dashboard loads without error
- [ ] Confirm: Sync no longer shows `Error` status after the fix is deployed

**Gate 5 — Committed to GitHub**
- [ ] Changes are committed to `bugfix/BUG-006-unable-to-sync-projects` with message:
      `bugfix: BUG-006 — fix invalid EmailCiphertext written by DbContextFactory`
- [ ] `git status` is clean before presenting merge commands

**Gate 6 — TASKS.md updated**
- [ ] `TASKS.md` updated to mark BUG-006 as `[DONE]` with date and resolution summary
- [ ] TASKS.md change committed in the same batch as the fix

**Gate 7 — Production deploy**
- [ ] Gates 1–6 are all confirmed before deploying
- [ ] UAT validated by developer on production after deploy
- [ ] Present the following merge and deploy commands to the developer for manual execution — do not execute them:
  ```
  git checkout BugFix-PC
  git merge bugfix/BUG-006-unable-to-sync-projects
  git checkout main
  git merge BugFix-PC
  git push
  ```
  Then run `publish-draftview.ps1` to deploy to production.

## Rules
- Do not change any production code until the plan has been reviewed and approved by the developer
- TDD sequence is non-negotiable: failing test → confirm red → fix → confirm green
- No null guards, no defensive try/catch, no "check before decrypt" workarounds — fix the root cause
- The fix must guarantee that a newly created Author row always has a valid ciphertext regardless of which code path creates the `DbContext`
- Existing tests must not be modified to make the new tests pass
- All git commands are presented to the developer for manual execution — never executed automatically
- A task is not complete until every Success Gate is confirmed — partial completion is not completion
