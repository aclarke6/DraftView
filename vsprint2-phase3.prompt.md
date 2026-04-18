---
mode: agent
description: V-Sprint 2 Phase 3 — Reader Diff Highlighting
---

# V-Sprint 2 / Phase 3 — Reader Diff Highlighting

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 9 and V-Sprint 2 Phase 3
2. Read `.github/copilot-instructions.md`
3. Read `.github/instructions/versioning.instructions.md`
4. Read `DraftView.Web/Controllers/ReaderController.cs` — understand `DesktopRead` and `MobileRead`
5. Read `DraftView.Web/Models/ReaderViewModels.cs` — understand `SceneWithComments` and `MobileReadViewModel`
6. Read `DraftView.Web/Views/Reader/DesktopRead.cshtml` — understand the scene prose render block
7. Read `DraftView.Web/Views/Reader/MobileRead.cshtml` — understand the prose render block
8. Confirm the active branch is `vsprint-2--phase-3-reader-highlighting`
   — if not on this branch, stop and report
9. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Goal

Readers see highlighted diff paragraphs when they open a section that has changed since
they last read it. Added paragraphs are highlighted green, removed paragraphs are shown
struck-through in red. Unchanged paragraphs render normally.

The diff is always-on in this sprint — no toggle required.
`LastReadVersionNumber` is already being set (V-Sprint 1 Phase 4).

---

## TDD

Controller changes that involve data resolution logic benefit from TDD.
View-only changes (CSS class rendering in Razor) do not require TDD.

---

## Existing Patterns — Follow These Exactly

- `ReaderController` uses `ISectionVersionRepository` (already injected, V-Sprint 1)
- `SceneWithComments.ResolvedHtmlContent` carries the HTML to render (V-Sprint 1 Phase 4)
- `ReadEvent.LastReadVersionNumber` is set on section open (V-Sprint 1 Phase 4)
- `ISectionDiffService` is registered in DI (Phase 2)
- CSS: new classes added to existing stylesheet with comment. No new stylesheet files.
  No inline styles.
- Style leakage audit required on any modified view

---

## Deliverable 1 — ViewModel Extension

**File:** `DraftView.Web/Models/ReaderViewModels.cs`

Add `DiffParagraphs` to `SceneWithComments`:

```csharp
/// <summary>
/// Paragraph-level diff results when the reader has a prior read version
/// and a newer version exists. Empty when no diff or no changes.
/// When non-empty, the view renders diff paragraphs instead of ResolvedHtmlContent.
/// </summary>
public IReadOnlyList<ParagraphDiffResult> DiffParagraphs { get; set; }
    = Array.Empty<ParagraphDiffResult>();

/// <summary>
/// True when DiffParagraphs contains highlighted changes to show the reader.
/// </summary>
public bool HasDiff => DiffParagraphs.Any(p => p.Type != DiffResultType.Unchanged);
```

Add `DiffParagraphs` and `HasDiff` to `MobileReadViewModel`:

```csharp
/// <summary>
/// Paragraph-level diff results for this scene. Empty when no changes.
/// </summary>
public IReadOnlyList<ParagraphDiffResult> DiffParagraphs { get; set; }
    = Array.Empty<ParagraphDiffResult>();

/// <summary>True when the reader has changes to see.</summary>
public bool HasDiff => DiffParagraphs.Any(p => p.Type != DiffResultType.Unchanged);
```

---

## Deliverable 2 — Controller: Resolve Diff

**File:** `DraftView.Web/Controllers/ReaderController.cs`

Add `ISectionDiffService sectionDiffService` to the constructor parameter list.

### `DesktopRead` private method

After resolving `ResolvedHtmlContent` for each scene, compute the diff:

```csharp
// Load the reader's last read version number for this scene
var readEvent = await readEventRepo.GetAsync(scene.Id, user.Id);
var lastReadVersionNumber = readEvent?.LastReadVersionNumber;

// Compute diff
var diffResult = await sectionDiffService.GetDiffForReaderAsync(
    scene.Id, lastReadVersionNumber);

scenesWithComments.Add(new SceneWithComments
{
    Scene               = scene,
    Comments            = displayComments,
    ResolvedHtmlContent = resolvedHtml,
    DiffParagraphs      = diffResult?.HasChanges == true
        ? diffResult.Paragraphs
        : Array.Empty<ParagraphDiffResult>()
});
```

You will need `IReadEventRepository` to load the read event. Check if it is already
available in `ReaderController` or needs injecting. Read the constructor before modifying.

### `MobileRead` private method

Apply the same pattern for the single scene.

---

## Deliverable 3 — View: Render Diff Highlighting

**File:** `DraftView.Web/Views/Reader/DesktopRead.cshtml`

Find the scene prose render block:

```razor
<div class="scene-prose-content prose">
    @Html.Raw(item.ResolvedHtmlContent ?? "<p><em>...</em></p>")
</div>
```

Replace with:

```razor
<div class="scene-prose-content prose">
    @if (item.HasDiff)
    {
        @foreach (var para in item.DiffParagraphs)
        {
            if (para.Type == DiffResultType.Added)
            {
                <div class="diff-para diff-para--added">@Html.Raw(para.Html)</div>
            }
            else if (para.Type == DiffResultType.Removed)
            {
                <div class="diff-para diff-para--removed">@Html.Raw(para.Html)</div>
            }
            else
            {
                @Html.Raw(para.Html)
            }
        }
    }
    else
    {
        @Html.Raw(item.ResolvedHtmlContent ?? "<p><em>The author has not added content to this scene yet.</em></p>")
    }
</div>
```

**File:** `DraftView.Web/Views/Reader/MobileRead.cshtml`

Apply the same replacement to the mobile prose block:

```razor
<div class="mobile-read__prose">
    @if (Model.HasDiff)
    {
        @foreach (var para in Model.DiffParagraphs)
        {
            if (para.Type == DiffResultType.Added)
            {
                <div class="diff-para diff-para--added">@Html.Raw(para.Html)</div>
            }
            else if (para.Type == DiffResultType.Removed)
            {
                <div class="diff-para diff-para--removed">@Html.Raw(para.Html)</div>
            }
            else
            {
                @Html.Raw(para.Html)
            }
        }
    }
    else
    {
        @Html.Raw(Model.ResolvedHtmlContent ?? "<p><em>The author has not added content to this scene yet.</em></p>")
    }
</div>
```

Style leakage audit: after editing both views, confirm no `style=""` attributes introduced.

---

## Deliverable 4 — CSS

**File:** `DraftView.Web/wwwroot/css/DraftView.DesktopReader.css`
(or `DraftView.Core.css` if diff styles are needed in both desktop and mobile)

Add diff highlight classes with a comment indicating they belong to reader diff views:

```css
/* Reader diff highlighting — DesktopRead.cshtml / MobileRead.cshtml */
.diff-para--added {
    background: var(--color-diff-added-bg, #f0fdf4);
    border-left: 3px solid var(--color-diff-added, #22c55e);
    padding-left: var(--space-3);
    margin-bottom: var(--space-2);
}

.diff-para--removed {
    background: var(--color-diff-removed-bg, #fef2f2);
    border-left: 3px solid var(--color-diff-removed, #ef4444);
    padding-left: var(--space-3);
    margin-bottom: var(--space-2);
    text-decoration: line-through;
    opacity: 0.7;
}
```

Bump the CSS version token in `DraftView.Core.css` using regex replace.
Update the CSS version in `_Layout.cshtml` to match.

---

## Deliverable 5 — DI Verification

Verify `ISectionDiffService` is registered in `ServiceCollectionExtensions.cs` (Phase 2).
Do not add a duplicate.

If `IReadEventRepository` needs injecting into `ReaderController`, verify it is already
registered in DI. Do not add a duplicate.

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

## Phase Gate — All Must Pass Before Marking Complete

Run `dotnet test --nologo` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline
- [ ] Solution builds without errors
- [ ] `SceneWithComments.DiffParagraphs` and `HasDiff` exist
- [ ] `MobileReadViewModel.DiffParagraphs` and `HasDiff` exist
- [ ] `ReaderController` injects `ISectionDiffService`
- [ ] `DesktopRead.cshtml` renders diff paragraphs when `HasDiff` is true
- [ ] `MobileRead.cshtml` renders diff paragraphs when `HasDiff` is true
- [ ] `.diff-para--added` and `.diff-para--removed` CSS classes added to stylesheet
- [ ] CSS version token bumped
- [ ] No inline styles introduced in any view
- [ ] Style leakage audit completed on both modified views
- [ ] TASKS.md Phase 3 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-2--phase-3-reader-highlighting`
- [ ] No warnings in test output linked to phase changes
- [ ] Refactor considered and applied where appropriate, tests green after refactor

---


## Do NOT implement in this phase

- Dismissible diff view — V-Sprint 3
- Update banner — V-Sprint 3
- "Updated since you last read" message — V-Sprint 3
- Change classification label — V-Sprint 4
- AI summary — V-Sprint 5
- Any author-facing changes
