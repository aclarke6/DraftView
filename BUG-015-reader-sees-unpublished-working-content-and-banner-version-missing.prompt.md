---
mode: agent
description: BUG-015 — Reader shows unpublished working content after sync and update banner does not display version number
---

# BUG-015 — Reader shows unpublished working content after sync and update banner does not display version number

## Branching
1. Checkout `BugFix-PC` (or `BugFix-Mac` if on Mac) and pull latest from `main`
2. Create and checkout `bugfix/BUG-015-reader-sees-unpublished-working-content-and-banner-version-missing` from `BugFix-PC`
3. All work is done on `bugfix/BUG-015-reader-sees-unpublished-working-content-and-banner-version-missing`
4. When all Success Gates pass, present the merge commands to the developer — do not execute them
5. Developer merges: `bugfix/BUG-015-...` → `BugFix-PC` → `main`

## Symptoms
1. Test project is reset so all prior `SectionVersion` rows are removed.
2. Author syncs and republishes a chapter, creating fresh Version 1 rows for its scenes.
3. Author then makes additional changes to a scene and syncs again without republishing.
4. Reader opens the scene and sees prose that was added after the last republish.
5. Database evidence shows only Version 1 exists for the scene, created before the later sync changes.
6. Expected behavior: reader must see only the latest persisted `SectionVersion.HtmlContent`, not newer working-state `Section.HtmlContent`.
7. Reader also sees an update banner, but the banner does not visibly show the version number.
8. This breaks the core publishing contract: synced working changes are leaking to readers before republish.

## Where to Start Looking
Read the following files in the order given. Do not write any code yet.

1. `DraftView.Web/Controllers/ReaderController.cs`
   - Read `DesktopRead`, `MobileRead`, `BuildSceneWithCommentsAsync`, and `ResolveSceneContentAndDiffAsync`
   - Ask: where is reader prose sourced from when a `SectionVersion` exists?
   - Ask: under what condition does the view render diff output instead of resolved version-backed prose?
   - Ask: is the diff pipeline capable of surfacing newer working-state content to readers?

2. `DraftView.Web/Models/ReaderViewModels.cs`
   - Read `SceneWithComments`
   - Ask: what fields drive prose rendering, diff rendering, update messaging, and the version label?
   - Ask: is `CurrentVersionNumber` present and intended for the banner?

3. `DraftView.Web/Models/MobileReaderViewModels.cs`
   - Read `MobileReadViewModel`
   - Ask: is the mobile reader model carrying the same version-backed and diff-backed state as desktop?

4. `DraftView.Web/Views/Reader/DesktopRead.cshtml`
   - Read the prose rendering block and the update banner block
   - Ask: when `HasDiff` is true, what content is rendered?
   - Ask: is the version number actually rendered in markup, and if so why might it not be visible?

5. `DraftView.Web/Views/Reader/MobileRead.cshtml`
   - Read the prose rendering block and the update banner block
   - Ask: does mobile follow the same rendering logic as desktop?
   - Ask: is the version label present in the markup?

6. `DraftView.Application/Services/ReadingProgressService.cs`
   - Read `RecordOpenAsync`, `UpdateLastReadVersionAsync`, and `DismissBannerAsync`
   - Ask: does reader progress update timing affect the diff/banner path?
   - Ask: is `LastReadVersionNumber` updated at the correct point in the read flow?

7. `DraftView.Application/Services/SectionDiffService.cs`
   - Read the full implementation
   - Ask: what two content sources are being diffed for a reader?
   - Ask: is it comparing latest published version to current working state?
   - Ask: should unpublished working content ever be exposed to the reader-facing diff output?

8. `DraftView.Application/Services/HtmlDiffService.cs`
   - Read how paragraph diff output is generated
   - Ask: if upstream passes working-state HTML, would the diff output include unpublished prose?

9. `DraftView.Web.Tests/Controllers/ReaderControllerTests.cs`
   - Read existing tests covering read flow, banner behavior, and rendered content
   - Ask: what reader behaviors are already covered and what is missing?

10. `DraftView.Application.Tests/Services/SectionDiffServiceTests.cs`
    - Read current diff tests
    - Ask: do any tests prove that reader-facing diffs are constrained to published versions only?

11. `Publishing And Versioning Architecture.md`
    - Read the reader model and working-state vs published-state sections
    - Ask: what is the explicit contract for reader content source and update messaging?

## What to Produce — Plan First, Then Pause
After reading all files, produce a written plan containing all four sections below.
Stop after the plan. Do not write any code. Wait for the plan to be reviewed and approved by the developer.

### Section 1 — Root Cause Analysis
State precisely:
- Why a reader can see prose that exists only in `Section.HtmlContent` after sync and before republish
- Whether the leak occurs in direct content resolution, the diff pipeline, or both
- Whether the reader view is rendering diff output derived from unpublished working content
- Why the version number is not visibly shown in the banner even though the system has persisted `SectionVersion.VersionNumber`
- Whether the banner issue is a data-flow problem, a rendering problem, or a styling/visibility problem

### Section 2 — Failing Test Plan
List each test that must be written. For each test state:
- the test class and method name
- what it seeds / arranges
- what it calls
- what assertion it makes
- why that assertion currently fails (red)
- why it will pass after the fix (green)

Tests must cover:
- Reader opening a scene with an existing `SectionVersion` sees published version content, not newer working-state content
- Reader does not see unpublished synced prose via diff rendering
- Update banner shows the current version number when a version exists
- Dismissing the banner persists correctly at the current version
- Desktop and mobile reader paths behave consistently
- Existing pre-versioning fallback behavior remains correct for sections with no `SectionVersion`

### Section 3 — Proposed Fix
Describe the fix in plain English before touching any code. State:
- which file(s) change and why
- whether the fix belongs in `SectionDiffService`, `ReaderController`, the views, or a combination
- how the reader will remain constrained to latest published content only
- how the version number will be made visibly present in the banner
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
- [ ] Reset the test project and create fresh Version 1 rows by republishing
- [ ] Make a further scene edit, sync only, do not republish
- [ ] Reader opens the scene and sees only the published Version 1 prose
- [ ] Reader does not see the newly synced but unpublished prose
- [ ] Update banner visibly shows the version number
- [ ] Banner dismiss works and stays dismissed after reload

**Gate 5 — Committed to GitHub**
- [ ] Committed to `bugfix/BUG-015-reader-sees-unpublished-working-content-and-banner-version-missing` with message:
      `bugfix: BUG-015 — keep reader content pinned to published versions and show banner version number`
- [ ] `git status` is clean

**Gate 6 — TASKS.md updated**
- [ ] `TASKS.md` updated to mark BUG-015 as `[DONE]` with date and resolution summary
- [ ] Included in same commit batch

## Approved Plan Addendum (2026-04-21)

### Root cause clarification

- The reader prose leak is in the diff-rendering path. `DesktopRead.cshtml` / `MobileRead.cshtml` render `DiffParagraphs` when `HasDiff` is true, which can override version-backed prose.
- The only way unpublished synced prose appears before republish is that the diff path returns paragraph content not strictly bounded to persisted `SectionVersion` data.
- For this bugfix, the reader must remain pinned to published prose whenever a `SectionVersion` exists.

### Banner version clarification

- Version number is already passed and rendered in markup; this is not treated as a CSS-token issue.
- Fix focus is data/conditional flow:
  - ensure banner rendering is only evaluated when a current version exists,
  - ensure desktop/mobile render paths consistently include a visible version token.

### Additional mandatory test

G) Diff integrity — no new prose beyond published version
- Class: `SectionDiffServiceTests`
- Method: `GetDiffForReaderAsync_DoesNotIntroduceContentBeyondLatestPublishedVersion`
- Arrange: working `Section.HtmlContent` contains additional text not present in latest `SectionVersion`
- Act: call diff service
- Assert: diff input/output is bounded to persisted versions; no paragraph content originates solely from working state
- Why red now: reader leak indicates diff path may include working-state content
- Why green: diff behavior constrained to persisted version data

### Prose rendering rule (mandatory)

When a `SectionVersion` exists:
- `ResolvedHtmlContent` is the only source of prose rendering
- `DiffParagraphs` may support update signaling but must never replace prose body content

**Gate 7 — Present merge commands**
- [ ] Present for manual execution — do not execute: