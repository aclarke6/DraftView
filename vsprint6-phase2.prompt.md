---
mode: agent
description: V-Sprint 6 Phase 2 — Publishing Page UI
---

# V-Sprint 6 / Phase 2 — Publishing Page UI

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 7, 8 and V-Sprint 6 Phase 2
2. Read `REFACTORING.md` in full
3. Read `.github/copilot-instructions.md`
4. Read `DraftView.Web/Controllers/AuthorController.cs` — understand existing publish actions
5. Read `DraftView.Web/Views/Author/Sections.cshtml` — understand current chapter controls
6. Read `DraftView.Domain/Interfaces/Services/IVersioningService.cs` — understand Phase 1 output
7. Read `DraftView.Web/Models/AuthorViewModels.cs` — understand existing ViewModels
8. Confirm the active branch is `vsprint-6--phase-2-publishing-page`
   — if not on this branch, stop and report
9. Run `git status` — confirm the working tree is clean with no uncommitted changes.
   If uncommitted changes exist that are not part of this phase, stop and report.
10. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Goal

Introduce a dedicated Publishing Page (`Author/Publishing/{projectId}`) that gives
the author granular control over publishing per chapter and per document.

The Publishing Page replaces the Republish button on the Sections view for published
chapters with changed content. The Sections view retains the Publish and Unpublish
buttons for unpublished chapters.

The Publishing Page shows:
- Each chapter with its publication status
- Republish and Revoke actions per chapter
- Expandable per-document controls when a chapter contains multiple documents
- For Manual projects: per-document controls always shown

---

## Architecture

This is a controller + view change only. All publishing logic lives in
`IVersioningService` (Phase 1). The controller is a thin HTTP surface.

---

## TDD

Controller actions that orchestrate multiple service calls benefit from tests.
View-only rendering does not require TDD. Assess per action.

---

## Deliverable 1 — ViewModels

**File:** `DraftView.Web/Models/AuthorViewModels.cs`

Add the following ViewModels to the existing file:

```csharp
/// <summary>
/// Top-level view model for the Publishing Page.
/// </summary>
public class PublishingPageViewModel
{
    public Project Project { get; init; } = default!;
    public IReadOnlyList<PublishingChapterViewModel> Chapters { get; init; } = [];
}

/// <summary>
/// Represents a chapter (Folder section) on the Publishing Page.
/// </summary>
public class PublishingChapterViewModel
{
    public Section Chapter { get; init; } = default!;
    public bool HasChanges { get; init; }
    public ChangeClassification? Classification { get; init; }
    public bool CanRevoke { get; init; }
    public bool ShowDocumentControls { get; init; }
    public IReadOnlyList<PublishingDocumentViewModel> Documents { get; init; } = [];
}

/// <summary>
/// Represents a Document section on the Publishing Page.
/// Shown when a chapter has multiple documents or for Manual projects.
/// </summary>
public class PublishingDocumentViewModel
{
    public Section Document { get; init; } = default!;
    public int? CurrentVersionNumber { get; init; }
    public bool HasChanges { get; init; }
    public ChangeClassification? Classification { get; init; }
    public bool CanRevoke { get; init; }
}
```

---

## Deliverable 2 — `AuthorController.Publishing` Action

**File:** `DraftView.Web/Controllers/AuthorController.cs`

Add a `Publishing` GET action:

```csharp
// ---------------------------------------------------------------------------
// Publishing Page
// ---------------------------------------------------------------------------
[HttpGet]
public async Task<IActionResult> Publishing(Guid projectId)
{
    var (author, error) = await RequireCurrentAuthorAsync();
    if (error is not null || author is null) return error ?? Forbid();

    var project = await projectRepo.GetByIdAsync(projectId);
    if (project is null) return NotFound();

    var sections = await sectionRepo.GetByProjectIdAsync(projectId);
    var sorted   = SortDepthFirst(sections);

    var folderChildIds = sorted
        .Where(x => x.Section.NodeType == NodeType.Folder && x.Section.ParentId.HasValue)
        .Select(x => x.Section.ParentId!.Value)
        .ToHashSet();

    var chapters = new List<PublishingChapterViewModel>();

    foreach (var (chapter, _) in sorted.Where(x =>
        x.Section.NodeType == NodeType.Folder &&
        x.Section.IsPublished &&
        !folderChildIds.Contains(x.Section.Id)))
    {
        var documents = sorted
            .Where(x => x.Section.ParentId == chapter.Id &&
                        x.Section.NodeType == NodeType.Document &&
                        !x.Section.IsSoftDeleted)
            .Select(x => x.Section)
            .ToList();

        var docViewModels = new List<PublishingDocumentViewModel>();
        foreach (var doc in documents)
        {
            var latestVersion = await sectionVersionRepo.GetLatestAsync(doc.Id);
            var allVersions   = latestVersion is not null
                ? await sectionVersionRepo.GetAllBySectionIdAsync(doc.Id)
                : [];

            ChangeClassification? classification = null;
            if (latestVersion is not null && doc.ContentChangedSincePublish)
            {
                try
                {
                    var diff = htmlDiffService.Compute(
                        latestVersion.HtmlContent,
                        doc.HtmlContent ?? string.Empty);
                    classification = changeClassificationService.Classify(diff);
                }
                catch { /* advisory only */ }
            }

            docViewModels.Add(new PublishingDocumentViewModel
            {
                Document             = doc,
                CurrentVersionNumber = latestVersion?.VersionNumber,
                HasChanges           = doc.ContentChangedSincePublish,
                Classification       = classification,
                CanRevoke            = allVersions.Count > 1
            });
        }

        var chapterHasChanges     = documents.Any(d => d.ContentChangedSincePublish);
        var chapterAllVersions    = new List<SectionVersion>();
        foreach (var doc in documents)
            chapterAllVersions.AddRange(await sectionVersionRepo.GetAllBySectionIdAsync(doc.Id));

        ChangeClassification? chapterClassification = null;
        if (chapterHasChanges && docViewModels.Any(d => d.Classification.HasValue))
            chapterClassification = docViewModels
                .Where(d => d.Classification.HasValue)
                .Max(d => d.Classification!.Value);

        var showDocControls = documents.Count > 1 ||
                              project.ProjectType == ProjectType.Manual;

        chapters.Add(new PublishingChapterViewModel
        {
            Chapter            = chapter,
            HasChanges         = chapterHasChanges,
            Classification     = chapterClassification,
            CanRevoke          = docViewModels.Any(d => d.CanRevoke),
            ShowDocumentControls = showDocControls,
            Documents          = docViewModels
        });
    }

    return View(new PublishingPageViewModel
    {
        Project  = project,
        Chapters = chapters
    });
}
```

---

## Deliverable 3 — `RepublishDocument` and `RevokeDocument` Actions

**File:** `DraftView.Web/Controllers/AuthorController.cs`

Add POST actions for per-document operations:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RepublishDocument(Guid sectionId, Guid projectId)
{
    var (author, error) = await RequireCurrentAuthorAsync();
    if (error is not null || author is null) return error ?? Forbid();

    try
    {
        await versioningService.RepublishSectionAsync(sectionId, author.Id);
        TempData["Success"] = "Document republished.";
    }
    catch (Exception ex)
    {
        TempData["Error"] = ex.Message;
    }

    return Redirect(Url.Action("Publishing", new { projectId }) + "#section-" + sectionId);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RevokeDocument(Guid sectionId, Guid projectId)
{
    var (author, error) = await RequireCurrentAuthorAsync();
    if (error is not null || author is null) return error ?? Forbid();

    try
    {
        await versioningService.RevokeLatestVersionAsync(sectionId, author.Id);
        TempData["Success"] = "Version revoked.";
    }
    catch (Exception ex)
    {
        TempData["Error"] = ex.Message;
    }

    return Redirect(Url.Action("Publishing", new { projectId }) + "#section-" + sectionId);
}
```

---

## Deliverable 4 — `Publishing.cshtml` View

**File:** `DraftView.Web/Views/Author/Publishing.cshtml`

Create a new view. No inline styles. Reference existing CSS classes where possible.

```razor
@model PublishingPageViewModel
@using DraftView.Domain.Enumerations
@{
    ViewData["Title"] = "Publishing — " + Model.Project.Name;
}

<h2>@Model.Project.Name — Publishing</h2>
<p><a asp-action="Sections" asp-route-projectId="@Model.Project.Id">&larr; Back to Sections</a></p>
<p class="page-subtitle">
    Republish chapters or individual documents. Revoke rolls back to the previous version.
</p>

@if (!Model.Chapters.Any())
{
    <p>No published chapters found. Publish a chapter from the Sections view first.</p>
}
else
{
    @foreach (var chapter in Model.Chapters)
    {
        <div class="publishing-chapter" id="section-@chapter.Chapter.Id">
            <div class="publishing-chapter__header">
                <span class="publishing-chapter__title">@chapter.Chapter.Title</span>

                @if (chapter.Classification.HasValue)
                {
                    <span class="change-indicator change-indicator--@chapter.Classification.Value.ToString().ToLower()">
                        @chapter.Classification.Value
                    </span>
                }

                <div class="publishing-chapter__actions">
                    @if (chapter.HasChanges)
                    {
                        <form asp-action="RepublishChapter" method="post" style="display:inline">
                            @Html.AntiForgeryToken()
                            <input type="hidden" name="chapterId" value="@chapter.Chapter.Id" />
                            <input type="hidden" name="projectId" value="@Model.Project.Id" />
                            <button type="submit" class="btn btn--republish btn--sm">Republish Chapter</button>
                        </form>
                    }
                </div>
            </div>

            @if (chapter.ShowDocumentControls)
            {
                <div class="publishing-documents">
                    @foreach (var doc in chapter.Documents)
                    {
                        <div class="publishing-doc" id="section-@doc.Document.Id">
                            <span class="publishing-doc__title">@doc.Document.Title</span>

                            @if (doc.CurrentVersionNumber.HasValue)
                            {
                                <span class="publishing-doc__version">v@doc.CurrentVersionNumber</span>
                            }

                            @if (doc.Classification.HasValue)
                            {
                                <span class="change-indicator change-indicator--@doc.Classification.Value.ToString().ToLower()">
                                    @doc.Classification.Value
                                </span>
                            }

                            <div class="publishing-doc__actions">
                                @if (doc.HasChanges)
                                {
                                    <form asp-action="RepublishDocument" method="post" style="display:inline">
                                        @Html.AntiForgeryToken()
                                        <input type="hidden" name="sectionId" value="@doc.Document.Id" />
                                        <input type="hidden" name="projectId" value="@Model.Project.Id" />
                                        <button type="submit" class="btn btn--republish btn--sm">Republish</button>
                                    </form>
                                }

                                @if (doc.CanRevoke)
                                {
                                    <form asp-action="RevokeDocument" method="post" style="display:inline">
                                        @Html.AntiForgeryToken()
                                        <input type="hidden" name="sectionId" value="@doc.Document.Id" />
                                        <input type="hidden" name="projectId" value="@Model.Project.Id" />
                                        <button type="submit" class="btn btn--danger btn--sm">Revoke</button>
                                    </form>
                                }
                            </div>
                        </div>
                    }
                </div>
            }
        </div>
    }
}
```

Style leakage audit: confirm no `style=""` attributes introduced — remove any that exist.
The one `style="display:inline"` on forms is pre-existing pattern — leave as-is.

---

## Deliverable 5 — Link to Publishing Page from Sections View

**File:** `DraftView.Web/Views/Author/Sections.cshtml`

For published chapters with `ContentChangedSincePublish`, replace the Republish button
with a link to the Publishing Page:

Find the existing Republish button block:

```razor
@if (s.ContentChangedSincePublish)
{
    <form asp-action="RepublishChapter" method="post" style="display:inline">
        ...
        <button type="submit" class="btn btn--republish btn--sm">Republish</button>
    </form>
}
```

Replace with:

```razor
@if (s.ContentChangedSincePublish)
{
    <a asp-action="Publishing"
       asp-route-projectId="@project!.Id"
       class="btn btn--republish btn--sm">
        Publish changes
    </a>
}
```

This routes the author to the Publishing Page instead of directly republishing.
The classification indicator from V-Sprint 4 remains in place next to this link.

---

## Deliverable 6 — CSS

**File:** `DraftView.Web/wwwroot/css/DraftView.Core.css`

Add Publishing Page styles with a comment:

```css
/* Publishing Page — Author/Publishing.cshtml */
.publishing-chapter {
    border: 1px solid var(--color-border);
    border-radius: var(--radius-md, 6px);
    padding: var(--space-4);
    margin-bottom: var(--space-4);
}

.publishing-chapter__header {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    margin-bottom: var(--space-3);
}

.publishing-chapter__title {
    font-weight: 600;
    font-size: var(--text-base);
    flex: 1;
}

.publishing-chapter__actions {
    display: flex;
    gap: var(--space-2);
}

.publishing-documents {
    border-top: 1px solid var(--color-border);
    padding-top: var(--space-3);
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
}

.publishing-doc {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    padding: var(--space-2) 0;
}

.publishing-doc__title {
    flex: 1;
    font-size: var(--text-sm);
}

.publishing-doc__version {
    font-size: var(--text-xs);
    color: var(--color-text-muted);
}

.publishing-doc__actions {
    display: flex;
    gap: var(--space-2);
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
- [ ] `PublishingPageViewModel`, `PublishingChapterViewModel`, `PublishingDocumentViewModel` exist
- [ ] `AuthorController.Publishing` GET action exists and returns `PublishingPageViewModel`
- [ ] `AuthorController.RepublishDocument` POST action exists
- [ ] `AuthorController.RevokeDocument` POST action exists
- [ ] `Publishing.cshtml` view exists with chapter and document controls
- [ ] `Sections.cshtml` Republish button replaced with "Publish changes" link to Publishing Page
- [ ] Classification indicators shown on Publishing Page
- [ ] CSS classes added for Publishing Page layout
- [ ] CSS version token bumped
- [ ] No new inline styles introduced in `Publishing.cshtml`
- [ ] Style leakage audit completed on all modified views
- [ ] TASKS.md Phase 2 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-6--phase-2-publishing-page`
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
quality, as per the refactoring guidelines. In particular:

- `Publishing` action assembles a complex view model — consider extracting a
  `BuildPublishingChapterViewModel` helper if the method exceeds 30 lines.
- Ensure all methods remain under the 30-line threshold per REFACTORING.md section 2.

---

## Do NOT implement in this phase

- Version history list on Publishing Page — V-Sprint 9
- Chapter-level Revoke — out of scope (per-document Revoke only)
- Scheduling or locking — V-Sprint 7
- Any reader-facing changes
