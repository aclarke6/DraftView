---
mode: agent
description: BUG-012 — Adding a new scene to a published chapter does not trigger a republish prompt
---

# BUG-012 — Adding a new scene to a published chapter does not trigger a republish prompt

## Branching
1. Checkout `BugFix-PC` (or `BugFix-Mac` if on Mac) and pull latest from `main`
2. Create and checkout `bugfix/BUG-012-new-scene-in-published-chapter-no-republish-prompt` from `BugFix-PC`
3. All work is done on `bugfix/BUG-012-new-scene-in-published-chapter-no-republish-prompt`
4. When all Success Gates pass, present the merge commands to the developer — do not execute them
5. Developer merges: `bugfix/BUG-012-...` → `BugFix-PC` → `main`

## Symptoms
1. Author has a published chapter (e.g. Chapter 2 with Scene 1 published)
2. Author adds a new scene (Scene 2) to that chapter in Scrivener and saves
3. Scrivener syncs to Dropbox. DraftView sync picks up the new scene — Scene 2 appears in the Sections view
4. The chapter shows no change indicator, no Republish button, and no prompt on the Publishing page
5. The author has no way to know the chapter needs republishing to make Scene 2 visible to readers
6. `ContentChangedSincePublish` is false on the chapter because no existing published content changed — only a new unpublished scene was added

## Root Cause to Investigate

Read the following files in the order given. Do not write any code yet.

1. `DraftView.Application/Services/ScrivenerSyncService.cs`
   - Read `ReconcileProjectFromScrivxAsync` and `ReconcileNodeAsync`
   - Ask: when a new Section row is created for a new binder node, is `ContentChangedSincePublish` set on the parent chapter?
   - Ask: is there any check after reconciliation that detects unpublished scenes under a published chapter?

2. `DraftView.Domain/Entities/Section.cs`
   - Read `MarkContentChanged` and `ContentChangedSincePublish`
   - Ask: is there a method or property that captures "this published chapter has unpublished child scenes"?

3. `DraftView.Application/Services/ScrivenerSyncService.cs` — `DetectContentChangesAsync`
   - Read this method fully
   - Ask: does it detect structural changes (new scenes) or only content changes (modified RTF)?

4. `DraftView.Web/Views/Author/Sections.cshtml`
   - Read the logic that shows the Republish button and change indicator
   - Ask: what conditions must be true for the Republish button to appear?

5. `DraftView.Web/Views/Author/Publishing.cshtml`
   - Read the logic that shows republish controls per chapter
   - Ask: what conditions must be true for a chapter to appear with republish controls?

## What to Produce — Plan First, Then Pause

After reading all files, produce a written plan containing **all four sections** below.
**Stop after the plan. Do not write any code. Wait for the plan to be reviewed and approved by the developer.**

### Section 1 — Root Cause Analysis
State precisely:
- Why `ContentChangedSincePublish` is not set when a new scene is added to a published chapter
- Whether the fix belongs in `ReconcileNodeAsync`, `DetectContentChangesAsync`, or elsewhere
- What the correct signal is: "this published chapter has at least one unpublished child scene"
- Whether this requires a new flag, a new method, or a change to how `ContentChangedSincePublish` is set

### Section 2 — Failing Test Plan
List each test that must be written. For each test state:
- The test class and method name
- What it seeds / arranges
- What it calls
- What assertion it makes
- Why that assertion currently fails (red)
- Why it will pass after the fix (green)

Tests must cover:
- That after sync, a published chapter with a newly added unpublished scene shows a change indicator
- That the Republish button appears on the Sections view for such a chapter
- That a published chapter with no new scenes and no content changes shows no change indicator
- That republishing the chapter clears the indicator and makes the new scene visible

### Section 3 — Proposed Fix
Describe the fix in plain English before touching any code. State:
- Which file(s) change and why
- What the change is — specifically how the system detects "published chapter has unpublished child scenes"
- What the change is NOT (no workarounds, no polling, no UI-only fix)
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
- [ ] Add a new scene to a published chapter in Scrivener, sync
- [ ] Sections view shows a change indicator and Republish button on the chapter
- [ ] Publishing page shows the chapter needs republishing
- [ ] After republishing, reader can see the new scene
- [ ] Change indicator clears after republish

**Gate 5 — Committed to GitHub**
- [ ] Committed to `bugfix/BUG-012-new-scene-in-published-chapter-no-republish-prompt` with message:
      `bugfix: BUG-012 — mark published chapter as changed when new unpublished scene is added`
- [ ] `git status` is clean

**Gate 6 — TASKS.md updated**
- [ ] `TASKS.md` updated to mark BUG-012 as `[DONE]` with date and resolution summary
- [ ] Included in same commit batch

**Gate 7 — Present merge commands**
- [ ] Present for manual execution — do not execute:
  ```
  git checkout BugFix-PC
  git merge bugfix/BUG-012-new-scene-in-published-chapter-no-republish-prompt
  git checkout main
  git merge BugFix-PC
  git push
  ```
  Then run `publish-draftview.ps1` to deploy to production.

## Rules
- Do not change any production code until the plan has been reviewed and approved by the developer
- TDD: failing test → confirm red → fix → confirm green
- No UI-only workarounds — the fix must be in the sync/reconciliation pipeline
- Existing tests must not be modified to make new tests pass
- All git commands presented to developer for manual execution — never executed automatically
- A task is not complete until every Success Gate is confirmed
