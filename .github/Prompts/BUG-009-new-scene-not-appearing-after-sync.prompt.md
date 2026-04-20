---
mode: agent
description: BUG-009 — New scene added in Scrivener does not appear in DraftView after sync
---

# BUG-009 — New scene added in Scrivener does not appear after sync

## Branching
1. Checkout `BugFix-PC` (or `BugFix-Mac` if on Mac) and pull latest from `main`
2. Create and checkout `bugfix/BUG-009-new-scene-not-appearing-after-sync` from `BugFix-PC`
3. All work is done on `bugfix/BUG-009-new-scene-not-appearing-after-sync`
4. When all Success Gates pass, present the merge commands to the developer — do not execute them
5. Developer merges: `bugfix/BUG-009-...` → `BugFix-PC` → `main`

## Symptoms
1. Author adds a new scene to an existing chapter in Scrivener and saves
2. Scrivener syncs to Dropbox — Dropbox confirms up to date
3. DraftView background sync runs and reports processing entries (e.g. "processed 13 entries")
4. The new scene does not appear in the Sections view
5. Querying the database confirms the new Section row was never created:
   ```sql
   SELECT "Title", "NodeType", "ScrivenerUuid" FROM "Sections"
   WHERE "ScrivenerUuid" = '{new-scene-uuid}';
   -- Returns 0 rows
   ```
6. The updated `.scrivx` file IS present in the local cache — it is newer than `version.txt`
7. The new scene's RTF content file may or may not be present in the cache

## Where to Start Looking

Read the following files in the order given. Do not write any code yet.

1. `DraftView.Application/Services/ScrivenerSyncService.cs`
   - Read `ParseProjectAsync` in full
   - Trace the complete call sequence: Dropbox download → scrivx parse → section upsert
   - Ask: is the scrivx re-parsed on every sync, or only when the scrivx file itself is listed as changed?
   - Ask: what triggers a Section row to be created vs updated?

2. `DraftView.Infrastructure/Sync/ScrivenerProjectParser.cs` (or equivalent parser)
   - Read how the binder tree is walked
   - Ask: does the parser read from the cached `.scrivx` file on disk, or from a Dropbox API response?
   - Ask: what happens when a new `BinderItem` UUID is encountered that has no existing Section row?

3. `DraftView.Infrastructure/Dropbox/DropboxFileDownloader.cs` (or `IDropboxFileDownloader`)
   - Read the incremental listing logic
   - Ask: does the incremental listing return the `.scrivx` file as a changed entry when a new scene is added?
   - Ask: is the scrivx guaranteed to be in the changed entries list when a child node is added?

4. `DraftView.Application/Services/ScrivenerSyncService.cs` — second pass
   - Focus on what happens after the incremental listing returns entries
   - Ask: is `ParseProjectAsync` always called, or only when specific file types are detected in the changed entries?

5. `DraftView.Domain/Interfaces/Repositories/ISectionRepository.cs` and its implementation
   - Read the upsert / create path for Section rows
   - Ask: is there any condition that would silently skip creating a new section?

## What to Produce — Plan First, Then Pause

After reading all files, produce a written plan containing **all four sections** below.
**Stop after the plan. Do not write any code. Wait for the plan to be reviewed and approved by the developer.**

### Section 1 — Root Cause Analysis
State precisely:
- Whether the scrivx is re-parsed on every sync cycle or only when detected as changed
- Whether the incremental listing from Dropbox reliably includes the scrivx when a child node is added
- Which code path is responsible for creating new Section rows from new BinderItem nodes
- Why the new Section row was not created despite the scrivx being updated in the cache
- Whether this is a Dropbox incremental detection gap, a parse trigger gap, or a section creation gap

### Section 2 — Failing Test Plan
List each test that must be written. For each test state:
- The test class and method name
- What it seeds / arranges
- What it calls
- What assertion it makes
- Why that assertion currently fails (red)
- Why it will pass after the fix (green)

Tests must cover:
- That adding a new BinderItem UUID to the scrivx results in a new Section row after `ParseProjectAsync`
- That the sync pipeline creates the new Section regardless of whether the scrivx appears in the incremental changed entries list
- That existing sections are not duplicated or modified when a new sibling is added

### Section 3 — Proposed Fix
Describe the fix in plain English before touching any code. State:
- Which file(s) change and why
- What the change is
- What the change is NOT (confirm no workarounds, no full-sync fallbacks unless genuinely required)
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
- [ ] Add a new scene in Scrivener, sync to Dropbox, trigger sync in DraftView
- [ ] Confirm the new scene appears in the Sections view without a restart
- [ ] Confirm existing sections and published state are unaffected

**Gate 5 — Committed to GitHub**
- [ ] Changes committed to `bugfix/BUG-009-new-scene-not-appearing-after-sync` with message:
      `bugfix: BUG-009 — new scene not created in database after incremental sync`
- [ ] `git status` is clean before presenting merge commands

**Gate 6 — TASKS.md updated**
- [ ] `TASKS.md` updated to mark BUG-009 as `[DONE]` with date and resolution summary
- [ ] TASKS.md change committed in the same batch as the fix

**Gate 7 — Production deploy**
- [ ] Gates 1–6 are all confirmed before deploying
- [ ] UAT validated by developer on production after deploy
- [ ] Present the following merge and deploy commands to the developer for manual execution — do not execute them:
  ```
  git checkout BugFix-PC
  git merge bugfix/BUG-009-new-scene-not-appearing-after-sync
  git checkout main
  git merge BugFix-PC
  git push
  ```
  Then run `publish-draftview.ps1` to deploy to production.

## Rules
- Do not change any production code until the plan has been reviewed and approved by the developer
- TDD sequence is non-negotiable: failing test → confirm red → fix → confirm green
- No null guards, no defensive fallbacks, no "force full re-sync" workarounds — fix the root cause
- Existing tests must not be modified to make new tests pass
- All git commands are presented to the developer for manual execution — never executed automatically
- A task is not complete until every Success Gate is confirmed — partial completion is not completion
