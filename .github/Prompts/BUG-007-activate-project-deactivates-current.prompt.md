---
mode: agent
description: BUG-007 — Activating a project does not deactivate the currently active project
---

# BUG-007 — Activating a project does not deactivate the currently active project

## Branching
1. Checkout `BugFix-PC` (or `BugFix-Mac` if on Mac) and pull latest from `main`
2. Create and checkout `bugfix/BUG-007-activate-project-deactivates-current` from `BugFix-PC`
3. All work is done on `bugfix/BUG-007-activate-project-deactivates-current`
4. When all Success Gates pass, present the merge commands to the developer — do not execute them
5. Developer merges: `bugfix/BUG-007-...` → `BugFix-PC` → `main`

## Symptoms
1. Author has one project already reader-active
2. Author clicks Activate on a second project
3. Both projects now show Reader Active = Active on the Dashboard
4. Two projects are simultaneously reader-active — violates the domain invariant

## Where to Start Looking

Read the following files in the order given. Do not write any code yet.

1. `DraftView.Web/Controllers/AuthorController.cs` — `ActivateProject` action
   - Note what it calls on the project entity
   - Ask: does it check for or deactivate any currently active project before activating the new one?

2. `DraftView.Domain/Entities/Project.cs` — `ActivateForReaders` method
   - Ask: does the domain method enforce the one-active-project invariant?
   - Ask: should this be enforced at domain level or application/service level?

3. `DraftView.Domain/Interfaces/Repositories/IProjectRepository.cs`
   - Ask: is there a `GetReaderActiveProjectAsync` method available?

4. `DraftView.Application/Services/` — check if a `ProjectService` or similar exists that handles activation
   - If it exists, read it fully
   - If activation logic lives only in the controller, note that

## What to Produce — Plan First, Then Pause

After reading all files, produce a written plan containing **all four sections** below.
**Stop after the plan. Do not write any code. Wait for the plan to be reviewed and approved by the developer.**

### Section 1 — Root Cause Analysis
State precisely:
- Where the activation logic currently lives
- Why the currently active project is not deactivated before the new one is activated
- Whether this should be fixed at the domain, application, or controller layer — and why

### Section 2 — Failing Test Plan
List each test that must be written. For each test state:
- The test class and method name
- What it seeds / arranges
- What it calls
- What assertion it makes
- Why that assertion currently fails (red)
- Why it will pass after the fix (green)

Tests must cover:
- Activating a project when no project is currently active — succeeds, one project active
- Activating a project when another project is currently active — the previously active project is deactivated, the new one is activated, exactly one active project remains
- That the invariant cannot be violated through the activation path

### Section 3 — Proposed Fix
Describe the fix in plain English before touching any code. State:
- Which layer owns the fix and why
- What the change is
- Whether a migration is required

### Section 4 — Success Gates
Present this checklist explicitly and confirm every item before declaring the task complete.

**Gate 1 — New tests are red before the fix**
- [ ] All new tests confirmed red — paste failing output

**Gate 2 — New tests are green after the fix**
- [ ] All new tests pass — paste passing output

**Gate 3 — No regressions**
- [ ] Full suite run — paste count: X passing, 0 failed, 1 skipped

**Gate 4 — Browser verification**
- [ ] With two projects, activating the second deactivates the first
- [ ] Only one project shows Reader Active on the Dashboard

**Gate 5 — Committed to GitHub**
- [ ] Committed to `bugfix/BUG-007-activate-project-deactivates-current` with message:
      `bugfix: BUG-007 — activating a project now atomically deactivates the current active project`
- [ ] `git status` is clean

**Gate 6 — TASKS.md updated**
- [ ] `TASKS.md` updated to mark BUG-007 as `[DONE]` with date and resolution summary
- [ ] Included in same commit batch

**Gate 7 — Present merge commands**
- [ ] Present for manual execution — do not execute:
  ```
  git checkout BugFix-PC
  git merge bugfix/BUG-007-activate-project-deactivates-current
  git checkout main
  git merge BugFix-PC
  git push
  ```
  Then run `publish-draftview.ps1` to deploy to production.

## Rules
- Fix at the correct architectural layer — not a controller workaround
- TDD: failing test → confirm red → fix → confirm green
- No null guards or defensive code as a first response — fix the invariant
- All git commands presented to developer for manual execution — never executed automatically
- A task is not complete until every Success Gate is confirmed
