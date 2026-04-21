---
mode: agent
description: BUG-016 — Publishing page shows raw Razor token instead of rendered version label
---

# BUG-016 — Publishing page shows raw Razor token instead of rendered version label

## Branching
1. Checkout `BugFix-PC` (or `BugFix-Mac` if on Mac) and pull latest from `main`
2. Create and checkout `bugfix/BUG-016-raw-version-label` from `BugFix-PC`
3. All work is done on `bugfix/BUG-016-raw-version-label`
4. When all Success Gates pass, present the merge commands to the developer — do not execute them
5. Developer merges: `bugfix/BUG-016-raw-version-label` → `BugFix-PC` → `main`

## Symptoms
1. On the Publishing page, a changed scene shows a literal raw token such as `v@doc.CurrentVersionNumber`
2. The version hint beside the scene action is not rendered as Razor output
3. This was observed during UAT on 2026-04-21 for Chapter 1 / Scene 2 after sync and before republish
4. Expected behavior: the Publishing page shows a rendered version label or hint, not raw template text

## Where to Start Looking
Read the following files in the order given. Do not write any code yet.

1. `DraftView.Web/Views/Author/Publishing.cshtml`
   - Read the full scene row/action rendering
   - Ask: where is the version label built?
   - Ask: is Razor expression output embedded incorrectly inside plain text or HTML?

2. `DraftView.Web/Models/AuthorViewModels.cs`
   - Read the view model used by the Publishing page
   - Ask: is the current version number already supplied to the view?
   - Ask: is there a dedicated property for the next/pending version label?

3. `DraftView.Web/Controllers/AuthorController.cs`
   - Read the Publishing page GET action
   - Ask: what version values are loaded into the Publishing page model?
   - Ask: is the controller passing the data the view needs?

4. `DraftView.Application/Services/VersioningService.cs`
   - Read any methods used to compute current or next version numbers
   - Ask: is there already a stable source for the version hint shown before republish?

5. `DraftView.Web.Tests/Controllers/AuthorControllerTests.cs`
   - Read existing Publishing page tests
   - Ask: is there already a rendered output or model-shape test for scene version hints?

## What to Produce — Plan First, Then Pause
After reading all files, produce a written plan containing all four sections below.
Stop after the plan. Do not write any code. Wait for the plan to be reviewed and approved by the developer.

### Section 1 — Root Cause Analysis
State precisely:
- Why the Publishing page shows the literal `v@doc.CurrentVersionNumber` token
- Whether the break is in Razor markup, view model shape, or controller mapping
- Whether the current version or next version value already exists in the model
- What exact rendering mistake causes raw Razor text to leak into the UI

### Section 2 — Failing Test Plan
List each test that must be written. For each test state:
- the test class and method name
- what it seeds / arranges
- what it calls
- what assertion it makes
- why that assertion currently fails (red)
- why it will pass after the fix (green)

Tests must cover:
- Publishing page scene row renders a real version label or hint, not a raw Razor token
- Current version number is present in the model when expected
- No regression to chapter-level Publishing page rendering
- No regression to scenes without a current version

### Section 3 — Proposed Fix
Describe the fix in plain English before touching any code. State:
- which file(s) change and why
- whether the fix is view-only or also needs controller/view-model adjustment
- what exact rendered output should appear after the fix
- what is explicitly not changing
- whether a migration is required

### Section 4 — Success Gates

**Gate 1 — New tests are red before the fix**
- [ ] All new tests confirmed red — paste failing output

**Gate 2 — New tests are green after the fix**
- [ ] All new tests pass — paste passing output

**Gate 3 — No regressions**
- [ ] Full suite run — paste count: X passing, 0 failed, 1 skipped

**Gate 4 — Browser verification**
- [ ] Open Publishing page for a changed chapter
- [ ] Confirm scene row shows a rendered version hint/label
- [ ] Confirm no raw Razor token is visible anywhere on the page
- [ ] Republish still works normally

**Gate 5 — Committed to GitHub**
- [ ] Committed to `bugfix/BUG-016-raw-version-label` with message:
      `bugfix: BUG-016 — fix raw version label rendering on publishing page`
- [ ] `git status` is clean

**Gate 6 — TASKS.md updated**
- [ ] `TASKS.md` updated to mark BUG-016 as `[DONE]` with date and resolution summary
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

