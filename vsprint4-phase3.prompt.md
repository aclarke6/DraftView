---
mode: agent
description: V-Sprint 4 Phase 3 — Author UI Indicator
---

# V-Sprint 4 / Phase 3 — Author UI Indicator

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 4.3 and V-Sprint 4 Phase 3
2. Read `REFACTORING.md` in full
3. Read `.github/copilot-instructions.md`
4. Read `DraftView.Web/Views/Author/Sections.cshtml` — understand the Republish button area
5. Read `DraftView.Web/Controllers/AuthorController.cs` — understand the `Sections` action
6. Read `DraftView.Application/Services/ChangeClassificationService.cs` — understand Phase 1
7. Read `DraftView.Domain/Interfaces/Services/IChangeClassificationService.cs`
8. Read `DraftView.Domain/Interfaces/Services/IHtmlDiffService.cs`
9. Read `DraftView.Domain/Interfaces/Repositories/ISectionVersionRepository.cs`
10. Confirm the active branch is `vsprint-4--phase-3-author-ui-indicator`
    — if not on this branch, stop and report
11. Run `git status` — confirm the working tree is clean with no uncommitted changes.
    If uncommitted changes exist that are not part of this phase, stop and report.
12. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Goal

Show a classification indicator next to the Republish button on the Author Sections
view, giving the author visibility into the nature of unpublished changes before
they click Republish.

The indicator is shown only when:
- The chapter is already published (`s.IsPublished`)
- `s.ContentChangedSincePublish` is true (there are unpublished changes)
- A classification can be computed (a previous version exists to diff against)

The indicator shows a colour-coded label: **Polish**, **Revision**, or **Rewrite**.
It is advisory only — it does not block or change the Republish action.

---

## Architecture

The classification for the indicator is computed at page-load time in the `Sections`
controller action, not during Republish. This is a read-only computation for display
purposes. The actual classification stored on `SectionVersion` is set during Republish
(Phase 2). These are two separate concerns.

The controller must not store the computed classification — it passes it to the view
via `ViewBag` or a ViewModel extension.

---

## TDD

Controller changes that involve data resolution logic benefit from TDD. View-only
rendering of already-computed values does not require TDD.

---

## Deliverable 1 — Extend `AuthorController.Sections`

**File:** `DraftView.Web/Controllers/AuthorController.cs`

The existing `Sections` action loads sections and builds a `publishable` set.
Extend it to also compute a classification indicator for chapters where
`IsPublished && ContentChangedSincePublish`.

Inject `IChangeClassificationService` and `IHtmlDiffService` into `AuthorController`
if not already present. Check the constructor before adding — do not duplicate.

Add `ISectionVersionRepository` injection if not already present.

Build a `Dictionary<Guid, ChangeClassification>` keyed by chapter section ID:

```csharp
var classificationMap = new Dictionary<Guid, ChangeClassification>();

foreach (var (s, _) in sorted.Where(x =>
    x.Section.NodeType == NodeType.Folder &&
    x.Section.IsPublished &&
    x.Section.ContentChangedSincePublish))
{
    // Get the document descendants of this chapter
    var documents = sorted
        .Where(x => x.Section.ParentId == s.Id &&
                    x.Section.NodeType == NodeType.Document &&
                    !x.Section.IsSoftDeleted)
        .Select(x => x.Section)
        .ToList();

    // Classify each document and take the highest classification
    var highestClassification = ChangeClassification.Polish;
    foreach (var doc in documents)
    {
        var latestVersion = await sectionVersionRepo.GetLatestAsync(doc.Id);
        if (latestVersion is null) continue;

        var diff = htmlDiffService.Compute(latestVersion.HtmlContent, doc.HtmlContent ?? string.Empty);
        var classification = changeClassificationService.Classify(diff);
        if (classification.HasValue && classification.Value > highestClassification)
            highestClassification = classification.Value;
    }

    if (documents.Any(d => sectionVersionRepo.GetLatestAsync(d.Id).Result is not null))
        classificationMap[s.Id] = highestClassification;
}

ViewBag.ClassificationMap = classificationMap;
```

**Important:** The `.Result` pattern above is illustrative only — use proper `await`
in an async loop. Read the existing method pattern for how other async operations
are performed in this action before implementing.

**Important:** Classification computation must be wrapped in try/catch — if it fails
for any section, skip that section silently and continue. Never throw from the
Sections page load.

---

## Deliverable 2 — View: Render Classification Indicator

**File:** `DraftView.Web/Views/Author/Sections.cshtml`

At the top of the view, retrieve the classification map from `ViewBag`:

```razor
var classificationMap = ViewBag.ClassificationMap as Dictionary<Guid, DraftView.Domain.Enumerations.ChangeClassification>
    ?? new Dictionary<Guid, DraftView.Domain.Enumerations.ChangeClassification>();
```

In the actions cell, where the Republish button is rendered
(inside `@if (s.ContentChangedSincePublish)`), add the indicator before the button:

```razor
@if (classificationMap.TryGetValue(s.Id, out var classification))
{
    <span class="change-indicator change-indicator--@classification.ToString().ToLower()">
        @classification
    </span>
}
```

The indicator appears inline next to the Republish button. No layout changes required.

Style leakage audit: confirm no `style=""` attributes introduced in the view.

---

## Deliverable 3 — CSS

**File:** `DraftView.Web/wwwroot/css/DraftView.Core.css`

Add classification indicator classes with a comment indicating they belong to
the Author Sections view:

```css
/* Change classification indicator — Author/Sections.cshtml */
.change-indicator {
    display: inline-flex;
    align-items: center;
    padding: 2px var(--space-2);
    border-radius: var(--radius-sm, 4px);
    font-size: var(--text-xs);
    font-family: var(--font-ui);
    font-weight: 500;
    margin-right: var(--space-1);
}

.change-indicator--polish {
    background: var(--color-polish-bg, #f0fdf4);
    color: var(--color-polish-text, #166534);
    border: 1px solid var(--color-polish-border, #bbf7d0);
}

.change-indicator--revision {
    background: var(--color-revision-bg, #fffbeb);
    color: var(--color-revision-text, #92400e);
    border: 1px solid var(--color-revision-border, #fde68a);
}

.change-indicator--rewrite {
    background: var(--color-rewrite-bg, #fef2f2);
    color: var(--color-rewrite-text, #991b1b);
    border: 1px solid var(--color-rewrite-border, #fecaca);
}
```

Bump the CSS version token in `DraftView.Core.css` using regex replace.
Update the CSS version in `_Layout.cshtml` to match.

---

## Phase Gate — All Must Pass Before Marking Complete

Run `dotnet test --nologo` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline
- [ ] Solution builds without errors
- [ ] `AuthorController.Sections` computes `classificationMap` for published chapters with changes
- [ ] Classification computation failure does not throw — silently skipped
- [ ] `Sections.cshtml` renders `.change-indicator` next to Republish button when classification available
- [ ] `.change-indicator--polish`, `.change-indicator--revision`, `.change-indicator--rewrite` CSS classes exist
- [ ] CSS version token bumped
- [ ] No inline styles introduced in any view
- [ ] Style leakage audit completed on `Sections.cshtml`
- [ ] TASKS.md Phase 3 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-4--phase-3-author-ui-indicator`
- [ ] No warnings in test output linked to phase changes
- [ ] Refactor considered and applied where appropriate, tests green after refactor

---

## Identify All Warnings in Tests

Run `dotnet test --nologo` and identify any warnings in the test output.
Address any warnings that are linked to code changes made in this phase before
proceeding, as they may indicate potential issues in the code.

---

## Refactor Phase

After implementing the above, consider if any refactor is needed to improve code
quality, as per the refactoring guidelines. If so, perform the refactor and ensure
all tests still pass.

---

## Do NOT implement in this phase

- Displaying classification to readers — not in this sprint
- Classification on the Republish confirmation step — V-Sprint 5
- Per-document classification indicators — V-Sprint 6
- Any changes to the reader views
- Any changes to `VersioningService`
