---
mode: agent
description: BUG-008 — Author/Section view has poor visual design and unreadable text
---

# BUG-008 — Author/Section view has poor visual design and unreadable text

## Branching
1. Checkout `BugFix-PC` (or `BugFix-Mac` if on Mac) and pull latest from `main`
2. Create and checkout `bugfix/BUG-008-author-section-view-visual-design` from `BugFix-PC`
3. All work is done on `bugfix/BUG-008-author-section-view-visual-design`
4. When all Success Gates pass, present the merge commands to the developer — do not execute them
5. Developer merges: `bugfix/BUG-008-...` → `BugFix-PC` → `main`

## Symptoms
1. Author clicks a scene title in the Sections view and lands on `Author/Section/{id}`
2. The content box renders with a white background against the dark theme
3. Text inside the content box is a very light colour — nearly invisible against the white background
4. The breadcrumb, metadata line, and comments section are also visually inconsistent with the rest of the author views
5. The overall page looks unfinished and jarring compared to all other author views

## What to Do

This is a CSS and view fix only. No domain, application, or controller changes are permitted.

Read these files before making any changes:

1. `DraftView.Web/Views/Author/Section.cshtml` — read the full view structure
2. `DraftView.Web/wwwroot/css/` — identify which stylesheet owns author view styles
3. Any existing author view that renders well (e.g. `Sections.cshtml`, `Dashboard.cshtml`) — note which CSS classes and variables they use for dark theme consistency

Then implement:

- Apply the dark theme consistently to the content box — use existing CSS variables, not hardcoded colours
- Fix text colour so prose content is readable against the dark background
- Style the breadcrumb, metadata line (`Revised Draft | Read by N reader(s)`), and comments section to match the visual language of other author views
- Do not add inline styles — all new CSS goes in the appropriate existing stylesheet with a comment indicating it belongs to `Author/Section`
- Bump the CSS version token via `Update-CssVersion.ps1` — never hardcode the current value

## Success Gates

**Gate 1 — No regressions**
- [ ] Full test suite passes — paste the count
- [ ] No existing tests modified

**Gate 2 — Browser verification**
- [ ] `Author/Section/{id}` renders with dark theme consistent with other author views
- [ ] Prose content is readable
- [ ] Breadcrumb, metadata, and comments section are visually consistent
- [ ] No inline styles added
- [ ] CSS version token bumped

**Gate 3 — Committed to GitHub**
- [ ] Changes committed to `bugfix/BUG-008-author-section-view-visual-design` with message:
      `bugfix: BUG-008 — restyle Author/Section view for dark theme consistency`
- [ ] `git status` is clean

**Gate 4 — TASKS.md updated**
- [ ] `TASKS.md` updated to mark BUG-008 as `[DONE]` with date and resolution summary
- [ ] Included in same commit batch

**Gate 5 — Present merge commands**
- [ ] Present the following for manual execution — do not execute:
  ```
  git checkout BugFix-PC
  git merge bugfix/BUG-008-author-section-view-visual-design
  git checkout main
  git merge BugFix-PC
  git push
  ```
  Then run `publish-draftview.ps1` to deploy to production.

## Rules
- CSS and view changes only — no domain, application, or controller changes
- No inline styles — all CSS in the appropriate existing stylesheet
- No new stylesheet files
- CSS version token must be bumped via `Update-CssVersion.ps1`
- All git commands presented to developer for manual execution — never executed automatically
