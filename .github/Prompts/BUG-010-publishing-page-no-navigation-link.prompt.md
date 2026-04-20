---
mode: agent
description: BUG-010 — Publishing page has no navigation link from Sections view or Dashboard
---

# BUG-010 — Publishing page has no navigation link from Sections view or Dashboard

## Branching
1. Checkout `BugFix-PC` (or `BugFix-Mac` if on Mac) and pull latest from `main`
2. Create and checkout `bugfix/BUG-010-publishing-page-no-navigation-link` from `BugFix-PC`
3. All work is done on `bugfix/BUG-010-publishing-page-no-navigation-link`
4. When all Success Gates pass, present the merge commands to the developer — do not execute them
5. Developer merges: `bugfix/BUG-010-...` → `BugFix-PC` → `main`

## Symptoms
1. Author publishes chapters and later edits content in Scrivener
2. After sync, the Sections view shows published chapters but no way to republish
3. The Publishing page (`Author/Publishing?projectId={id}`) exists and works but is only reachable by typing the URL directly
4. There is no link to the Publishing page from the Sections view or the Dashboard
5. An author who does not know the URL cannot republish changed chapters

## Where to Start Looking

Read the following files in the order given. Do not write any code yet.

1. `DraftView.Web/Views/Author/Sections.cshtml`
   - Read the full view
   - Note what is shown for published chapters that have changes (`ContentChangedSincePublish`)
   - Ask: is there any existing link or button that leads to the Publishing page?

2. `DraftView.Web/Views/Author/Publishing.cshtml`
   - Read the full view
   - Understand what it shows and what actions it provides
   - Note the "Back to Sections" link — confirm it exists and works

3. `DraftView.Web/Controllers/AuthorController.cs`
   - Read the `Sections` action — note how `ClassificationMap` and `Publishable` are passed to the view
   - Read the `Publishing` GET action — note the route and parameters
   - Read `RepublishChapter` POST action — note what it does and where it redirects

4. `DraftView.Web/Views/Author/Dashboard.cshtml` (or equivalent)
   - Note what project-level actions are available
   - Ask: is there a natural place to add a Publishing link per project?

## What to Produce — Plan First, Then Pause

After reading all four files, produce a written plan containing **all four sections** below.
**Stop after the plan. Do not write any code. Wait for the plan to be reviewed and approved by the developer.**

### Section 1 — Root Cause Analysis
State precisely:
- Where in the Sections view changed chapters are displayed
- Why there is no republish button or Publishing page link currently
- Whether `ContentChangedSincePublish` and `ClassificationMap` are already available in the Sections view model

### Section 2 — Proposed Changes
This is a view-only change — no domain or application layer changes are required. Describe:

**Sections view (`Sections.cshtml`):**
- Add a **Republish** button directly on the Sections view for any published chapter where `ContentChangedSincePublish = true` — this handles the common case without requiring navigation to the Publishing page
- Add a **Publishing** link for all published chapters — this gives access to advanced operations (locking, scheduling, version history)
- Show a change classification badge (Polish / Revision / Rewrite) next to the change indicator where available from `ClassificationMap`

**Dashboard (`Dashboard.cshtml`):**
- Add a **Publishing** link in the project actions column alongside Sync / Activate / Remove

State which CSS classes already exist for these elements and whether any new CSS is needed. Any new CSS must go in the appropriate existing stylesheet with a comment — no new stylesheet files.

### Section 3 — Failing Test Plan
This is primarily a view change. State:
- Whether any new controller logic is required (it should not be)
- If controller changes are needed, provide the full TDD test plan per the standard format
- If view-only, confirm that existing integration tests cover the Sections and Dashboard routes and that no new tests are strictly required — but note any test that should be added to verify the new links render correctly

### Section 4 — Success Gates
Present this checklist explicitly and confirm every item before declaring the task complete.
Do not proceed to the merge step until every gate is confirmed.

**Gate 1 — Tests**
- [ ] If any controller changes were made: new tests red before fix, green after, full suite passes
- [ ] If view-only: confirm full suite still passes — paste the count
- [ ] No regressions

**Gate 2 — Browser verification**
- [ ] Sections view shows Republish button on a published chapter with `ContentChangedSincePublish = true`
- [ ] Sections view shows Publishing link on all published chapters
- [ ] Clicking Republish on Sections view successfully republishes the chapter
- [ ] Clicking Publishing link navigates to the Publishing page for that project
- [ ] Dashboard shows Publishing link in project actions
- [ ] CSS version token bumped via `Update-CssVersion.ps1` if any CSS was changed

**Gate 3 — Committed to GitHub**
- [ ] Changes committed to `bugfix/BUG-010-publishing-page-no-navigation-link` with message:
      `bugfix: BUG-010 — add Republish button and Publishing link to Sections view and Dashboard`
- [ ] `git status` is clean before presenting merge commands

**Gate 4 — TASKS.md updated**
- [ ] `TASKS.md` updated to mark BUG-010 as `[DONE]` with date and resolution summary
- [ ] TASKS.md change committed in the same batch as the fix

**Gate 5 — Production deploy**
- [ ] Gates 1–4 are all confirmed before deploying
- [ ] UAT validated by developer on production after deploy
- [ ] Present the following merge and deploy commands to the developer for manual execution — do not execute them:
  ```
  git checkout BugFix-PC
  git merge bugfix/BUG-010-publishing-page-no-navigation-link
  git checkout main
  git merge BugFix-PC
  git push
  ```
  Then run `publish-draftview.ps1` to deploy to production.

## Rules
- This is a view-only change — do not introduce domain or application layer changes unless the plan review explicitly approves them
- Any new CSS must be added to the appropriate existing stylesheet with a comment indicating which view it belongs to — no new stylesheet files
- CSS version token must be bumped via `Update-CssVersion.ps1` if any CSS is changed — never hardcode the current value
- All git commands are presented to the developer for manual execution — never executed automatically
- A task is not complete until every Success Gate is confirmed — partial completion is not completion
