---
mode: agent
description: CHANGE-001 — Move desktop reader scene version label from main content area to left-hand navigation
---

# CHANGE-001 — Move desktop reader scene version label from main content area to left-hand navigation

## Branching
1. Checkout `Change-PC` (or `Change-Mac` if on Mac) and pull latest from `main`
2. Create and checkout `change/CHANGE-001-scene-version-nav` from `Change-PC`
3. All work is done on `change/CHANGE-001-scene-version-nav`
4. When all Success Gates pass, present the merge commands to the developer — do not execute them
5. Developer merges: `change/CHANGE-001-scene-version-nav` → `Change-PC` → `main`

## Change Request
The desktop reader currently shows the persistent scene version label in the main reading area beneath the scene heading.

Requested change:
- Move the desktop scene version label from the main scene title area into the left-hand scene navigation beside each scene heading.

This is a UI placement change, not a versioning logic change.

## Expected Behaviour
### Desktop
- The current version label for each scene should appear in the left-hand scene navigation
- The main scene content area should no longer display a duplicate persistent version label beneath the scene heading
- The version label should still only appear when a scene actually has a version
- The update banner behavior must remain unchanged

### Mobile
- Move the mobile scene version label out of the main reading pane title area to reduce reading noise
- Show the version label in the existing top navigation metadata row (`Prev / Scenes / Next`) as a compact center-aligned indicator
- Keep it visible only when a version exists; do not duplicate it in the prose/title block
- The update banner behavior must remain unchanged

## Where to Start Looking
Read the following files in the order given. Do not write code yet.

1. `DraftView.Web/Views/Reader/DesktopRead.cshtml`
 - Read the current sidebar scene navigation block
 - Read the current main scene heading/version block
 - Ask: what is the current desktop rendering structure for version labels?

2. `DraftView.Web/Views/Reader/MobileRead.cshtml`
 - Read the current mobile title/version placement
 - Ask: should mobile remain as-is, or should it move to a different existing metadata location?

3. `DraftView.Web/wwwroot/css/DraftView.DesktopReader.css`
 - Read the sidebar and scene metadata styling
 - Ask: what existing classes or patterns can support version display in the left-hand navigation?

4. `DraftView.Web/wwwroot/css/DraftView.MobileReader.css`
 - Read the mobile version label styling
 - Ask: is any mobile CSS change justified if mobile placement is reconsidered?

5. `DraftView.Web.Tests/Controllers/ReaderControllerTests.cs`
 - Read existing reader rendering tests
 - Ask: what current tests prove version labels render, and what will need changing if placement moves?

## What to Produce — Plan First, Then Pause
After reading all files, produce a written plan containing all four sections below.
Stop after the plan. Do not write any code. Wait for the plan to be reviewed and approved by the developer.

### Section 1 — Current State Analysis
State precisely:
- where the desktop version label is currently rendered
- where the desktop version label would move
- where the mobile version label is currently rendered
- where the mobile version label would move to match the same “lower noise” motivation
- whether any CSS change appears necessary

### Section 2 — Failing Test Plan
List each test that must be written or updated. For each test state:
- the test class and method name
- what it arranges
- what it renders
- what assertion it makes
- why that assertion currently fails (red)
- why it passes after the change (green)

Tests must cover:
- Desktop reader renders scene version label in the left-hand navigation
- Desktop reader no longer renders the persistent version label under the main scene heading
- Desktop reader still omits version label for scenes without versions
- Mobile reader behavior remains correct for the chosen design

### Section 3 — Proposed Fix
Describe the fix in plain English before touching any code. State:
- which files need to change
- what moves on desktop
- what moves on mobile (mirror desktop motivation: lower noise, persistent context)
- whether CSS changes are needed
- what is explicitly not changing
- whether a migration is required

### Section 4 — Success Gates

**Gate 1 — New tests are red before the fix**
- [ ] All new or updated tests confirmed red — paste failing output

**Gate 2 — New tests are green after the fix**
- [ ] All new or updated tests pass — paste passing output

**Gate 3 — No regressions**
- [ ] Full suite run — paste count

**Gate 4 — Browser verification**
- [ ] Desktop reader shows version label beside each scene in left-hand navigation
- [ ] Desktop main scene heading no longer shows duplicate persistent version label
- [ ] Mobile reader shows version label in the top navigation metadata area (not in main scene title block)
- [ ] Update banner behavior unchanged

**Gate 5 — Committed to GitHub**
- [ ] Committed to `change/CHANGE-001-scene-version-nav` with message:
    `change: move desktop scene version label into reader navigation`
- [ ] `git status` is clean

**Gate 6 — TASKS.md or change log updated if required**
- [ ] Updated only if your workflow requires tracking this change
- [ ] Included in same commit batch if applicable

**Gate 7 — Present merge commands**
- [ ] Present for manual execution — do not execute:
  ```
  git checkout Change-PC
  git pull origin main
  git merge change/CHANGE-001-scene-version-nav
  git checkout main
  git merge Change-PC
  git push origin main
  ```

