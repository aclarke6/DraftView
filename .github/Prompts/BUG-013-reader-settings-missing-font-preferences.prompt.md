---
mode: agent
description: BUG-013 — Reader Account Settings missing font and font size preferences
---

# BUG-013 — Reader Account Settings missing font and font size preferences

## Branching
1. Checkout `BugFix-PC` (or `BugFix-Mac` if on Mac) and pull latest from `main`
2. Create and checkout `bugfix/BUG-013-reader-settings-missing-font-preferences` from `BugFix-PC`
3. All work is done on `bugfix/BUG-013-reader-settings-missing-font-preferences`
4. When all Success Gates pass, present the merge commands to the developer — do not execute them
5. Developer merges: `bugfix/BUG-013-...` → `BugFix-PC` → `main`

## Symptoms
1. Reader navigates to `/Account/Settings`
2. Profile, Appearance (theme), Email Address, and Change Password sections are visible
3. Font and font size preference controls are missing entirely for readers
4. Expected behavior: readers can view and edit prose font and prose font size preferences built in Sprint 3

## Where to Start Looking
Read in this order. Do not write code yet.

1. `DraftView.Web/Controllers/AccountController.cs`
   - Read the settings GET action and POST action(s)
   - Ask: are reader preference values loaded into the settings model?
   - Ask: are posted reader preference values persisted for readers?

2. `DraftView.Web/Models/AccountSettingsViewModel.cs` (or the settings ViewModel file used by Account settings)
   - Read all properties used by the settings page
   - Ask: do properties for prose font and prose font size exist?
   - Ask: are they nullable/typed correctly for binding and validation?

3. `DraftView.Web/Views/Account/Settings.cshtml`
   - Read the Razor markup for settings sections and role-based conditionals
   - Ask: are prose font and prose size controls rendered at all?
   - Ask: are they accidentally hidden for readers due to conditionals?

4. `DraftView.Domain/Entities/UserPreferences.cs`
   - Read properties and defaults for prose font and prose font size
   - Ask: are defaults valid and aligned with UI options?

5. `DraftView.Domain/Interfaces/Repositories/IUserPreferencesRepository.cs` and implementation
   - Read retrieval and persistence path used by Account settings
   - Ask: does the read/write path include prose font and prose font size fields?

6. Any settings mapping helpers/service used by Account settings
   - Ask: is there a mapping omission between preferences entity and view model?

## What to Produce — Plan First, Then Pause
After reading all files, produce a written plan with all four sections below.
Stop after the plan. Do not write any code. Wait for developer approval.

### Section 1 — Root Cause Analysis
State precisely:
- Why reader settings page does not show prose font and prose font size controls
- Whether the break is in controller loading, ViewModel shape, Razor rendering, or mapping between them
- Why the issue affects readers specifically (if role-based)
- Whether persistence currently works but rendering is missing, or both are broken

### Section 2 — Failing Test Plan
For each test include:
- test class and method name
- arrange / act / assert
- why it fails now (red)
- why it passes after fix (green)

Must cover:
- Settings GET returns model including prose font and prose font size values for reader
- Settings view renders prose font and prose font size controls for reader
- Settings POST persists prose font and prose font size changes for reader
- Non-reader behavior remains unchanged where intended

### Section 3 — Proposed Fix
Describe in plain English:
- exact files to change and why
- minimal safe change to restore reader font controls
- whether fix is in controller/model/view mapping, view rendering, or both
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
- [ ] Login as reader and open `/Account/Settings`
- [ ] Prose font and prose font size controls are visible
- [ ] Change both values and save
- [ ] Reload settings and confirm saved values persist
- [ ] Open reader content page and confirm selected prose font/size are applied

**Gate 5 — Committed to GitHub**
- [ ] Committed to `bugfix/BUG-013-reader-settings-missing-font-preferences` with message:
      `bugfix: BUG-013 — restore reader prose font and font size controls in account settings`
- [ ] `git status` is clean

**Gate 6 — TASKS.md updated**
- [ ] `TASKS.md` updated to mark BUG-013 as `[DONE]` with date and resolution summary
- [ ] Included in same commit batch

**Gate 7 — Present merge commands**
- [ ] Present for manual execution — do not execute:
  ```
  git checkout BugFix-PC
  git merge bugfix/BUG-013-reader-settings-missing-font-preferences
  git checkout main
  git merge BugFix-PC
  git push
  ```
  Then run `publish-draftview.ps1` to deploy to production.

## Rules
- Do not change any production code until the plan is reviewed and approved by the developer
- TDD: failing test → confirm red → fix → confirm green
- No view-only workaround that bypasses proper settings load/save path
- Existing tests must not be modified to make new tests pass
- All git commands presented to developer for manual execution — never executed automatically
- A task is not complete until every Success Gate is confirmed
