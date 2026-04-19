---
mode: agent
description: V-Sprint 10 Phase 3 — Sync Project Tree Display
---

# V-Sprint 10 / Phase 3 — Sync Project Tree Display

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` V-Sprint 10 Phase 3
2. Read `REFACTORING.md` in full
3. Read `.github/copilot-instructions.md`
4. Read `DraftView.Web/Views/Author/Sections.cshtml` — understand current Sections view
5. Read `DraftView.Web/Controllers/AuthorController.cs` — understand `Sections` action
6. Read `DraftView.Domain/Entities/Section.cs` — understand `ContentChangedSincePublish`
7. Confirm the active branch is `vsprint-10--phase-3-sync-tree-display`
   — if not on this branch, stop and report
8. Run `git status` — confirm the working tree is clean with no uncommitted changes.
   If uncommitted changes exist that are not part of this phase, stop and report.
9. Run `.\test-summary.ps1` and record the baseline passing count before touching any code

---

## Goal

For `ScrivenerDropbox` projects, improve the Sections view to make it clear that
the tree structure is read-only (Scrivener controls it), and to surface structural
changes as incoming sync updates where relevant.

This is a display-only phase. No new domain logic, no new services, no schema
changes. Pure UI improvement for the sync project experience.

---

## Key Constraint

> For `ScrivenerDropbox` projects: tree is read-only.
> Structural changes shown as incoming sync updates.
> The tree builder controls (Add, Delete, Drag) must never appear for sync projects.

---

## Deliverable 1 — Read-Only Tree Indicators

**File:** `DraftView.Web/Views/Author/Sections.cshtml`

For `ScrivenerDropbox` projects:

1. Add a subtitle note below the existing page subtitle:
```razor
@if (project?.ProjectType == ProjectType.ScrivenerDropbox)
{
    <p class="page-subtitle page-subtitle--info">
        This project is managed by Scrivener. The section structure is updated
        automatically when you sync. To add, remove, or reorder sections,
        make the changes in Scrivener and sync again.
    </p>
}
```

2. In the Type column, add a sync indicator for sections that changed since the
   last sync (where `ContentChangedSincePublish` is true and the section is a Document):

```razor
@if (!isFolder && s.ContentChangedSincePublish && project?.ProjectType == ProjectType.ScrivenerDropbox)
{
    <span class="sync-change-badge">Updated</span>
}
```

3. Confirm the tree builder controls from Phase 2 (Add Section form, Delete button,
   drag handles) do not appear for `ScrivenerDropbox` projects. Verify the condition
   checks in the view are correct — do not add duplicate guards.

---

## Deliverable 2 — Sync Change Summary

**File:** `DraftView.Web/Views/Author/Sections.cshtml`

At the top of the sections table, for `ScrivenerDropbox` projects, show a count
of sections with unpublished changes if any exist:

```razor
@{
    var changedCount = project?.ProjectType == ProjectType.ScrivenerDropbox
        ? Model.Count(x => !x.Section.IsSoftDeleted &&
                            x.Section.NodeType == NodeType.Document &&
                            x.Section.ContentChangedSincePublish)
        : 0;
}
@if (changedCount > 0)
{
    <p class="sync-summary">
        @changedCount section@(changedCount == 1 ? "" : "s") updated since last publish.
    </p>
}
```

---

## Deliverable 3 — CSS

**File:** `DraftView.Web/wwwroot/css/DraftView.Core.css`

```css
/* Sync tree display — Author/Sections.cshtml (V-Sprint 10 Phase 3) */
.page-subtitle--info {
    color: var(--color-text-muted);
    font-style: italic;
}

.sync-change-badge {
    display: inline-block;
    padding: 1px var(--space-2);
    background: var(--color-info-bg, #eff6ff);
    color: var(--color-info-text, #1d4ed8);
    border: 1px solid var(--color-info-border, #bfdbfe);
    border-radius: var(--radius-sm, 4px);
    font-size: var(--text-xs);
    font-weight: 500;
    margin-left: var(--space-1);
}

.sync-summary {
    font-size: var(--text-sm);
    color: var(--color-text-muted);
    margin-bottom: var(--space-3);
}
```

Bump the CSS version token in `DraftView.Core.css` using regex replace.
Update the CSS version in `_Layout.cshtml` to match.

---

## Phase Gate — All Must Pass Before Marking Complete

Run `.\test-summary.ps1` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline
- [ ] Solution builds without errors
- [ ] Read-only note shown for `ScrivenerDropbox` projects
- [ ] `sync-change-badge` shown on Documents with `ContentChangedSincePublish` in sync projects
- [ ] Sync change summary count shown when changes exist
- [ ] Tree builder controls confirmed absent for `ScrivenerDropbox` projects
- [ ] CSS classes added
- [ ] CSS version token bumped
- [ ] No new inline styles in views
- [ ] Style leakage audit completed on `Sections.cshtml`
- [ ] TASKS.md Phase 3 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-10--phase-3-sync-tree-display`
- [ ] No warnings in test output linked to phase changes

---

## Do NOT implement in this phase

- Tree diff between Scrivener and local state
- Webhook-based sync triggers
- Any changes to the sync service or sync provider
- Any reader-facing changes
- Any domain or infrastructure changes
