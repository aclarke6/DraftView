---
mode: agent
description: BUG-014 — Republishing a chapter creates new versions for all scenes, not just changed ones
---

# BUG-014 — Republishing a chapter creates new versions for all scenes, not just changed ones

## Branching
1. Checkout `BugFix-PC` (or `BugFix-Mac` if on Mac) and pull latest from `main`
2. Create and checkout `bugfix/BUG-014-republish-creates-versions-for-unchanged-scenes` from `BugFix-PC`
3. All work is done on `bugfix/BUG-014-republish-creates-versions-for-unchanged-scenes`
4. When all Success Gates pass, present the merge commands to the developer — do not execute them
5. Developer merges: `bugfix/BUG-014-...` → `BugFix-PC` → `main`

## Symptoms
1. Author publishes a chapter with two scenes (Scene 1 Version 1, Scene 2 Version 1)
2. Author edits Scene 1 in Scrivener — only Scene 1 has changed
3. After sync, the chapter shows a change indicator and Republish button
4. Author clicks Republish
5. Both Scene 1 AND Scene 2 get a new version (Version 2) — even though Scene 2 has not changed
6. Reader sees "Updated — version 2" and "Updated since you last read" on Scene 2, which has not changed
7. On subsequent republishes, version numbers increment on all scenes every time regardless of changes
8. This misleads readers, wastes version quota, and erodes reader trust

## Where to Start Looking

Read the following files in the order given. Do not write any code yet.

1. `DraftView.Application/Services/VersioningService.cs` — `RepublishChapterAsync`
   - Read the full method
   - Note that `publishableDocuments` is all non-soft-deleted documents with HtmlContent
   - Note that `CreateVersionForDocumentAsync` is called for every document unconditionally
   - Ask: is there any check for whether `ContentChangedSincePublish` is true before creating a version?
   - Ask: is there any check for whether a version already exists before creating a new one?

2. `DraftView.Domain/Entities/Section.cs`
   - Read `ContentChangedSincePublish`, `PublishAsPartOfChapter`, and `MarkContentChanged`
   - Ask: does `PublishAsPartOfChapter` reset `ContentChangedSincePublish` to false?
   - Ask: after a first-publish, what is the value of `ContentChangedSincePublish`?

3. `DraftView.Application/Interfaces/Repositories/ISectionVersionRepository.cs`
   - Note `GetMaxVersionNumberAsync` and `GetAllBySectionIdAsync`
   - Ask: is there a method to check if any version exists for a given section?

4. `DraftView.Application.Tests/Services/VersioningServiceTests.cs`
   - Read existing republish tests
   - Understand the test seeding patterns used

## What to Produce — Plan First, Then Pause

After reading all files, produce a written plan containing **all four sections** below.
**Stop after the plan. Do not write any code. Wait for the plan to be reviewed and approved by the developer.**

### Section 1 — Root Cause Analysis
State precisely:
- Why `RepublishChapterAsync` creates versions for all documents unconditionally
- What condition should gate version creation for each document
- The two cases where a new version IS correct:
  1. Scene has `ContentChangedSincePublish = true` (content has changed)
  2. Scene has no existing versions (first publish of a new scene)
- The case where a new version is NOT correct:
  - Scene has `ContentChangedSincePublish = false` AND at least one version already exists
- Whether `PublishAsPartOfChapter` correctly resets `ContentChangedSincePublish` after the fix

### Section 2 — Failing Test Plan
List each test that must be written. For each test state:
- The test class and method name
- What it seeds / arranges
- What it calls
- What assertion it makes
- Why that assertion currently fails (red)
- Why it will pass after the fix (green)

Tests must cover:
- Republishing a chapter where only one scene has changed — only the changed scene gets a new version
- Republishing a chapter where a new scene has been added (no prior version) — new scene gets Version 1, unchanged scenes keep their current version
- Republishing a chapter where all scenes have changed — all scenes get new versions
- Republishing a chapter where no scenes have changed — no new versions created (or appropriate invariant thrown)

### Section 3 — Proposed Fix
Describe the fix in plain English before touching any code. State:
- The condition to add to `RepublishChapterAsync` before calling `CreateVersionForDocumentAsync`
- Whether `CreateVersionForDocumentAsync` itself should be modified or whether the guard belongs in `RepublishChapterAsync`
- What happens to `ContentChangedSincePublish` after the fix — confirm it is reset correctly
- Whether a migration is required

### Section 4 — Success Gates

**Gate 1 — New tests are red before the fix**
- [ ] All new tests confirmed red — paste failing output

**Gate 2 — New tests are green after the fix**
- [ ] All new tests pass — paste passing output

**Gate 3 — No regressions**
- [ ] Full suite run — paste count: X passing, 0 failed, 1 skipped

**Gate 4 — Browser verification**
- [ ] Edit one scene in a published two-scene chapter, sync, republish
- [ ] Only the edited scene gets a new version number
- [ ] Unedited scene keeps its current version — no "Updated" banner shown to reader
- [ ] Republish button clears after republishing

**Gate 5 — Committed to GitHub**
- [ ] Committed to `bugfix/BUG-014-republish-creates-versions-for-unchanged-scenes` with message:
      `bugfix: BUG-014 — only create new version for scenes changed since last publish`
- [ ] `git status` is clean

**Gate 6 — TASKS.md updated**
- [ ] `TASKS.md` updated to mark BUG-014 as `[DONE]` with date and resolution summary
- [ ] Included in same commit batch

**Gate 7 — Present merge commands**
- [ ] Present for manual execution — do not execute:
  ```
  git checkout BugFix-PC
  git merge bugfix/BUG-014-republish-creates-versions-for-unchanged-scenes
  git checkout main
  git merge BugFix-PC
  git push
  ```
  Then run `publish-draftview.ps1` to deploy to production.

## Rules
- Do not change any production code until the plan has been reviewed and approved by the developer
- TDD: failing test → confirm red → fix → confirm green
- The fix must be in `VersioningService.cs` — not a controller workaround or view-level hide
- `PublishAsPartOfChapter` must reset `ContentChangedSincePublish` correctly after the fix — confirm this explicitly
- Existing tests must not be modified to make new tests pass
- All git commands presented to developer for manual execution — never executed automatically
- A task is not complete until every Success Gate is confirmed
