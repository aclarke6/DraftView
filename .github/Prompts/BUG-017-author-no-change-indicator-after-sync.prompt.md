---
mode: agent
description: BUG-017 — Author Sections view does not clearly show pending scene change after sync
---

# BUG-017 — Author Sections view does not clearly show pending scene change after sync

## Branching
1. Checkout `BugFix-PC` (or `BugFix-Mac` if on Mac) and pull latest from `main`
2. Create and checkout `bugfix/BUG-017-change-indicator` from `BugFix-PC`
3. All work is done on `bugfix/BUG-017-change-indicator`
4. When all Success Gates pass, present the merge commands to the developer — do not execute them
5. Developer merges: `bugfix/BUG-017-change-indicator` → `BugFix-PC` → `main`

## Symptoms
1. Author edits Scene 2 in Scrivener and syncs successfully
2. Sections view does not show a clear pending-change indication before opening Publishing
3. Publishing page correctly identifies the scene as changed (`Polish`) and allows republish
4. This was observed during UAT on 2026-04-21
5. Expected behavior: after sync, the Author Sections view should clearly surface that a published chapter/scene has pending changes awaiting republish

## Where to Start Looking
Read the following files in the order given. Do not write any code yet.

1. `DraftView.Web/Views/Author/Sections.cshtml`
   - Read the full row rendering for chapters and scenes
   - Ask: what conditions currently show changed-state indicators or republish prompts?
   - Ask: is the changed state surfaced at chapter level, scene level, or both?

2. `DraftView.Web/Models/AuthorViewModels.cs`
   - Read the view model used by Sections
   - Ask: does it already carry changed-state or classification information needed by the view?
   - Ask: is any pending-change information missing from the model?

3. `DraftView.Web/Controllers/AuthorController.cs`
   - Read the Sections GET action
   - Ask: what changed-state signals are computed and passed into the Sections view?
   - Ask: is the controller dropping information that is available elsewhere?

4. `DraftView.Web/Views/Author/Publishing.cshtml`
   - Read how the Publishing page shows a changed scene (`Polish`, `Revision`, `Rewrite`)
   - Ask: what information exists there that Sections currently does not surface?

5. `DraftView.Application/Services/ScrivenerSyncService.cs`
   - Read the content-change detection and chapter/scene update path
   - Ask: after sync, what persisted flags or state should indicate pending unpublished change?

6. `DraftView.Domain/Entities/Section.cs`
   - Read `ContentChangedSincePublish` and related publish/change methods
   - Ask: is the persisted changed-state sufficient, and is the problem only in Author UI surfacing?

7. `DraftView.Web.Tests/Controllers/AuthorControllerTests.cs`
   - Read existing Sections view and Publishing page tests
   - Ask: is there any test proving pending-change indication appears after sync?

## What to Produce — Plan First, Then Pause
After reading all files, produce a written plan containing all four sections below.
Stop after the plan. Do not write any code. Wait for the plan to be reviewed and approved by the developer.

### Section 1 — Root Cause Analysis
State precisely:
- Why the Sections view does not clearly indicate a pending synced change before Publishing is opened
- Whether the missing signal is in persisted state, controller mapping, view model shape, or Razor rendering
- Why the Publishing page can show `Polish` while Sections fails to surface a clear indicator
- Whether the correct fix should surface chapter-level pending changes, scene-level pending changes, or both

### Section 2 — Failing Test Plan
List each test that must be written. For each test state:
- the test class and method name
- what it seeds / arranges
- what it calls
- what assertion it makes
- why that assertion currently fails (red)
- why it will pass after the fix (green)

Tests must cover:
- Sections view clearly indicates a changed published chapter after sync
- Sections view clearly indicates a changed scene when appropriate
- Publishing page behavior remains unchanged
- No false positive indicator appears when no content has changed

### Section 3 — Proposed Fix
Describe the fix in plain English before touching any code. State:
- which file(s) change and why
- how the Sections view will clearly surface pending change state after sync
- how the solution will stay aligned with existing persisted change data and Publishing page behavior
- what is explicitly not being changed
- whether a migration is required

### Section 4 — Success Gates

**Gate 1 — New tests are red before the fix**
- [ ] All new tests confirmed red — paste failing output

**Gate 2 — New tests are green after the fix**
- [ ] All new tests pass — paste passing output

**Gate 3 — No regressions**
- [ ] Full suite run — paste count: X passing, 0 failed, 1 skipped

**Gate 4 — Browser verification**
- [ ] Edit a published scene in Scrivener and sync
- [ ] Open Sections view
- [ ] Confirm a clear pending-change indication is visible before opening Publishing
- [ ] Open Publishing and confirm changed scene classification still appears correctly
- [ ] Republish and confirm the pending-change indication clears

**Gate 5 — Committed to GitHub**
- [ ] Committed to `bugfix/BUG-017-change-indicator` with message:
      `bugfix: BUG-017 — show pending synced changes clearly in sections view`
- [ ] `git status` is clean

**Gate 6 — TASKS.md updated**
- [ ] `TASKS.md` updated to mark BUG-017 as `[DONE]` with date and resolution summary
- [ ] Included in same commit batch

**Gate 7 — Present merge commands**
- [ ] Present for manual execution — do not execute:

## Rules
- Do not change any production code until the plan has been reviewed and approved by the developer
- TDD: failing test → confirm red → fix → confirm green
- No Publishing-page-only workaround — the fix must make the Sections view surface the existing change state correctly
- Existing tests must not be modified to make new tests pass
- All git commands presented to the developer for manual execution — never executed automatically
- A task is not complete until every Success Gate is confirmed