---
mode: agent
description: V-Sprint 10 Phase 2 — Tree Builder UI
---

# V-Sprint 10 / Phase 2 — Tree Builder UI

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` V-Sprint 10 Phase 2
2. Read `REFACTORING.md` in full
3. Read `.github/copilot-instructions.md`
4. Read `DraftView.Web/Views/Author/Sections.cshtml` — understand the current Sections view
5. Read `DraftView.Web/Controllers/AuthorController.cs` — understand existing author actions
6. Read `DraftView.Domain/Interfaces/Services/ISectionTreeService.cs` — Phase 1 output
7. Read `DraftView.Web/Models/AuthorViewModels.cs` — understand existing ViewModels
8. Confirm the active branch is `vsprint-10--phase-2-tree-builder-ui`
   — if not on this branch, stop and report
9. Run `git status` — confirm the working tree is clean with no uncommitted changes.
   If uncommitted changes exist that are not part of this phase, stop and report.
10. Run `.\test-summary.ps1` and record the baseline passing count before touching any code

---

## Goal

Add an explicit drag-and-drop section tree management UI for `Manual` projects.
For `ScrivenerDropbox` projects, the tree is read-only — Scrivener owns the structure.

The tree builder replaces the implicit section creation flow (Option B) with an
explicit UI (Option A) for manual projects. Both flows remain valid — the implicit
upload path (`GetOrCreateForUpload`) is unchanged.

---

## Scope

This phase is **Manual projects only**. `ScrivenerDropbox` projects show read-only
tree structure. The drag-and-drop reorder behaviour uses the HTML5 Drag and Drop API
with a lightweight JavaScript handler — no external drag-and-drop library required.

---

## Architecture

All tree mutations go through `ISectionTreeService` (Phase 1).
Controller actions are thin HTTP surfaces only — no business logic.

---

## Deliverable 1 — Controller Actions

**File:** `DraftView.Web/Controllers/AuthorController.cs`

Add the following POST actions. Read the existing constructor before modifying —
`ISectionTreeService` may already be injected. Do not duplicate.

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateSection(
    Guid projectId, string title, NodeType nodeType, Guid? parentId)

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> MoveSection(
    Guid sectionId, Guid projectId, Guid? newParentId, int newSortOrder)

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteSection(Guid sectionId, Guid projectId)
```

All three follow the same pattern:
1. `RequireCurrentAuthorAsync()`
2. Call the appropriate `ISectionTreeService` method
3. Set `TempData["Success"]` or `TempData["Error"]`
4. Redirect to `Sections` for the project

`MoveSection` should return `Ok()` rather than redirect — it is called via fetch
from the drag-and-drop JavaScript handler.

---

## Deliverable 2 — Sections View: Tree Builder for Manual Projects

**File:** `DraftView.Web/Views/Author/Sections.cshtml`

For `Manual` projects (`project.ProjectType == ProjectType.Manual`), show:

1. **Add Section button** — opens an inline form or modal to create a new section
   - Fields: Title (text), Type (Folder/Document radio), Parent (dropdown from existing sections)
   - POST to `CreateSection`

2. **Delete button** on each section row — POST to `DeleteSection` with confirmation

3. **Drag handle** on each section row — enables reordering via HTML5 drag and drop

For `ScrivenerDropbox` projects, none of these controls are shown. The tree is
read-only and Scrivener-controlled.

**Add Section inline form** (shown above the table for Manual projects):

```razor
@if (project?.ProjectType == ProjectType.Manual)
{
    <div class="tree-builder__add-form">
        <form asp-action="CreateSection" method="post">
            @Html.AntiForgeryToken()
            <input type="hidden" name="projectId" value="@project!.Id" />
            <input type="text" name="title" placeholder="Section title" class="input input--sm" required />
            <select name="nodeType" class="input input--sm">
                <option value="Document">Document</option>
                <option value="Folder">Chapter / Folder</option>
            </select>
            <select name="parentId" class="input input--sm">
                <option value="">— No parent (root) —</option>
                @foreach (var item in Model.Where(x => x.Section.NodeType == NodeType.Folder && !x.Section.IsSoftDeleted))
                {
                    <option value="@item.Section.Id">@item.Section.Title</option>
                }
            </select>
            <button type="submit" class="btn btn--primary btn--sm">Add Section</button>
        </form>
    </div>
}
```

**Drag and drop JavaScript** — add inline at the bottom of the view:

```javascript
// Tree builder drag-and-drop for Manual projects
(function () {
    var rows = document.querySelectorAll('tr[data-section-id][data-draggable]');
    var draggedId = null;
    rows.forEach(function (row) {
        row.setAttribute('draggable', 'true');
        row.addEventListener('dragstart', function () { draggedId = row.dataset.sectionId; });
        row.addEventListener('dragover', function (e) { e.preventDefault(); });
        row.addEventListener('drop', function (e) {
            e.preventDefault();
            if (!draggedId || draggedId === row.dataset.sectionId) return;
            var token = document.querySelector('input[name="__RequestVerificationToken"]').value;
            fetch('/Author/MoveSection', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token },
                body: 'sectionId=' + draggedId +
                      '&projectId=' + row.dataset.projectId +
                      '&newParentId=' + (row.dataset.parentId || '') +
                      '&newSortOrder=' + row.dataset.sortOrder +
                      '&__RequestVerificationToken=' + encodeURIComponent(token)
            }).then(function (r) { if (r.ok) location.reload(); });
        });
    });
})();
```

Add `data-section-id`, `data-draggable`, `data-project-id`, `data-parent-id`,
and `data-sort-order` attributes to each `<tr>` for Manual projects only.

---

## Deliverable 3 — CSS

**File:** `DraftView.Web/wwwroot/css/DraftView.Core.css`

```css
/* Tree builder UI — Author/Sections.cshtml (V-Sprint 10 Phase 2) */
.tree-builder__add-form {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    margin-bottom: var(--space-4);
    flex-wrap: wrap;
}

tr[draggable="true"] {
    cursor: grab;
}

tr[draggable="true"]:active {
    cursor: grabbing;
    opacity: 0.7;
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
- [ ] `CreateSection` POST action exists
- [ ] `MoveSection` POST action returns `Ok()` (not redirect)
- [ ] `DeleteSection` POST action exists
- [ ] Add Section form shown for Manual projects only
- [ ] Delete button shown per row for Manual projects only
- [ ] Drag handles shown for Manual projects only
- [ ] `ScrivenerDropbox` projects show no tree builder controls
- [ ] Drag-and-drop JavaScript calls `MoveSection` via fetch
- [ ] CSS classes added for tree builder
- [ ] CSS version token bumped
- [ ] No new inline styles in views
- [ ] Style leakage audit completed on `Sections.cshtml`
- [ ] TASKS.md Phase 2 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-10--phase-2-tree-builder-ui`
- [ ] No warnings in test output linked to phase changes
- [ ] Refactor considered and applied where appropriate, tests green after refactor

---

## Do NOT implement in this phase

- Sync project tree display — Phase 3
- Rename section in place
- Multi-select or bulk operations
- Any changes to the reader experience
- Any changes to `ScrivenerDropbox` project tree structure
