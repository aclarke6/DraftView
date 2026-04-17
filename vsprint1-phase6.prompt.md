---
mode: agent
description: V-Sprint 1 Phase 6 — Manual Upload UI
---

# V-Sprint 1 / Phase 6 — Manual Upload UI

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 3.4, 3.5, 10, and 11
2. Read `.github/copilot-instructions.md`
3. Read `DraftView.Web/Controllers/AuthorController.cs` — understand the existing action shapes
4. Read `DraftView.Web/Views/Author/Sections.cshtml` — understand the current table structure
5. Read `DraftView.Application/Services/SectionTreeService.cs` — understand `GetOrCreateForUploadAsync`
6. Read `DraftView.Application/Services/ImportService.cs` — understand `ImportAsync`
7. Confirm the active branch is `vsprint-1--phase-6-manual-upload-ui`
   — if not on this branch, stop and report
8. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Goal

Allow the author to upload an RTF file for a Manual project directly from the Sections view.
The uploaded file is converted to HTML via `IImportService` and written to `Section.HtmlContent`.
No versioning occurs on upload — the author triggers Republish separately (Phase 5).

Manual project sections have no Scrivener sync. The upload form is the only way to get
content into them.

---

## Two-Phase Context: What Exists vs What This Phase Adds

Already exists (Phases 1–5):
- `IImportService` + `ImportService` — converts RTF stream → HTML, writes to `Section.HtmlContent`
- `ISectionTreeService` + `SectionTreeService` — `GetOrCreateForUploadAsync` creates sections
- `IVersioningService` + `VersioningService` — creates `SectionVersion` snapshots
- `RepublishChapter` POST action on `AuthorController` (Phase 5)

This phase adds:
- `UploadScene` GET — renders the upload form
- `UploadScene` POST — accepts the file, calls `ISectionTreeService.GetOrCreateForUploadAsync`
  then `IImportService.ImportAsync`, redirects back to Sections
- Upload button on the Sections view for Manual project Document sections
- `UploadSceneViewModel`

---

## TDD

`UploadScene` POST involves service orchestration — TDD recommended.
`UploadScene` GET is a thin view return — no TDD required.
View changes (adding the upload button) do not require TDD.

---

## Existing Patterns — Follow These Exactly

- `AuthorController` primary constructor — add `IImportService` and `ISectionTreeService`
- All POST actions: `[HttpPost, ValidateAntiForgeryToken]`, call `RequireCurrentAuthorAsync()`
- `TempData["Success"]` and `TempData["Error"]` for feedback
- Redirect to `Sections` with `#section-{sectionId}` anchor after success
- CSS: any new class added to existing stylesheet with comment. No new stylesheet files.
  No inline styles.
- File upload uses `IFormFile` — check `ModelState.IsValid` before processing
- Style leakage audit required on any modified view

---

## Deliverable 1 — `UploadSceneViewModel`

**File:** `DraftView.Web/Models/AuthorViewModels.cs`

Add to the existing file — do not create a new file:

```csharp
/// <summary>
/// Form model for uploading an RTF file to a Manual project section.
/// Used by AuthorController.UploadScene GET and POST.
/// </summary>
public class UploadSceneViewModel
{
    public Guid ProjectId { get; set; }
    public Guid? ParentChapterId { get; set; }
    public string SceneTitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a file to upload.")]
    public IFormFile? File { get; set; }
}
```

No TDD required for a ViewModel.

---

## Deliverable 2 — `UploadScene` Controller Actions

**File:** `DraftView.Web/Controllers/AuthorController.cs`

Add `IImportService importService` and `ISectionTreeService sectionTreeService`
to the constructor parameter list.

### GET action

```csharp
[HttpGet]
public async Task<IActionResult> UploadScene(Guid projectId, Guid? parentChapterId)
{
    var project = await projectRepo.GetByIdAsync(projectId);
    if (project is null) return NotFound();

    return View(new UploadSceneViewModel
    {
        ProjectId       = projectId,
        ParentChapterId = parentChapterId
    });
}
```

### POST action

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> UploadScene(UploadSceneViewModel model)
{
    var (author, error) = await RequireCurrentAuthorAsync();
    if (error is not null || author is null) return error ?? Forbid();

    if (!ModelState.IsValid)
        return View(model);

    try
    {
        var section = await sectionTreeService.GetOrCreateForUploadAsync(
            model.ProjectId,
            model.SceneTitle,
            model.ParentChapterId,
            sortOrder: null);

        await using var stream = model.File!.OpenReadStream();
        await importService.ImportAsync(
            model.ProjectId,
            section.Id,
            stream,
            model.File.FileName,
            author.Id);

        TempData["Success"] = $"\"{model.SceneTitle}\" uploaded successfully.";
    }
    catch (UnsupportedFileTypeException ex)
    {
        TempData["Error"] = $"Unsupported file type: {ex.Extension}. Only RTF files are supported.";
        return View(model);
    }
    catch (Exception ex)
    {
        TempData["Error"] = ex.Message;
        return View(model);
    }

    return Redirect(Url.Action("Sections", new { projectId = model.ProjectId })
        + (model.ParentChapterId.HasValue ? "#section-" + model.ParentChapterId : string.Empty));
}
```

### TDD — `DraftView.Web.Tests`

Check if `AuthorControllerTests` or similar exists in `DraftView.Web.Tests`.
If it does, add tests there. If not, create
`DraftView.Web.Tests/Controllers/AuthorControllerUploadTests.cs`.

Write failing tests before implementing:

```
UploadScene_Post_CallsGetOrCreateForUploadAsync_WithCorrectParameters
UploadScene_Post_CallsImportAsync_WithCorrectSectionId
UploadScene_Post_SetsTempDataSuccess_WhenUploadSucceeds
UploadScene_Post_SetsTempDataError_WhenUnsupportedFileType
UploadScene_Post_ReturnsView_WhenModelStateInvalid
```

Study existing controller test patterns before writing.

---

## Deliverable 3 — `UploadScene` View

**File:** `DraftView.Web/Views/Author/UploadScene.cshtml`

Create this file:

```html
@model UploadSceneViewModel
@{
    ViewData["Title"] = "Upload Scene";
}

<h2>Upload Scene</h2>
<p><a asp-action="Sections" asp-route-projectId="@Model.ProjectId">&larr; Back to Sections</a></p>

<form asp-action="UploadScene" method="post" enctype="multipart/form-data" class="upload-form">
    @Html.AntiForgeryToken()
    <input type="hidden" asp-for="ProjectId" />
    <input type="hidden" asp-for="ParentChapterId" />

    <div class="form-group">
        <label asp-for="SceneTitle" class="form-label">Scene title</label>
        <input asp-for="SceneTitle" class="form-input" placeholder="e.g. Chapter 1, Scene 1" />
        <span asp-validation-for="SceneTitle" class="field-validation-error"></span>
    </div>

    <div class="form-group">
        <label asp-for="File" class="form-label">RTF file</label>
        <input asp-for="File" type="file" accept=".rtf" class="form-input" />
        <span asp-validation-for="File" class="field-validation-error"></span>
    </div>

    <div class="form-actions">
        <button type="submit" class="btn btn--primary">Upload</button>
        <a asp-action="Sections" asp-route-projectId="@Model.ProjectId" class="btn btn--secondary">Cancel</a>
    </div>
</form>

@section Scripts {
    @{await Html.RenderPartialAsync("_ValidationScriptsPartial");}
}
```

No inline styles. Use existing CSS classes only (`form-group`, `form-label`, `form-input`,
`form-actions`, `btn`, `btn--primary`, `btn--secondary`).

If any of these classes do not exist in the current CSS, add them to the appropriate
stylesheet with a comment indicating they belong to `Author/UploadScene.cshtml`.

---

## Deliverable 4 — Upload Button in Sections View

**File:** `DraftView.Web/Views/Author/Sections.cshtml`

The upload button appears on Document (scene) rows for Manual projects only.
Manual projects are identified by `project?.ProjectType == ProjectType.Manual`.

Add this check to the top of the `@foreach` block, alongside the existing `isChapter` check:

```csharp
var isManualDocument = !isFolder && project?.ProjectType == ProjectType.Manual;
```

In the `<td>` Actions column, after the existing chapter publish/unpublish block, add:

```html
@if (isManualDocument)
{
    <a asp-action="UploadScene"
       asp-route-projectId="@project!.Id"
       asp-route-parentChapterId="@s.ParentId"
       class="btn btn--secondary btn--sm">
        Upload
    </a>
}
```

**Style leakage audit:** after editing `Sections.cshtml`, audit the entire file for
`style=""` attributes. The existing file has several inline styles — report them but do
not fix them in this phase unless you introduced them. Only fix leakage on lines you touched.

---

## Deliverable 5 — DI Verification

`IImportService` and `ISectionTreeService` are already registered in DI (Phase 2).
Verify they appear in `ServiceCollectionExtensions.cs` — do not add duplicates.

`UnsupportedFileTypeException` is already in `DraftView.Domain.Exceptions` — import it.

---

## Phase Gate — All Must Pass Before Marking Complete

Run `dotnet test --nologo` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline
- [ ] Solution builds without errors
- [ ] `UploadSceneViewModel` exists in `AuthorViewModels.cs`
- [ ] `AuthorController` constructor includes `IImportService` and `ISectionTreeService`
- [ ] `UploadScene` GET action exists and returns view
- [ ] `UploadScene` POST action calls `GetOrCreateForUploadAsync` then `ImportAsync`
- [ ] `UploadScene.cshtml` exists with no inline styles
- [ ] Upload button appears in `Sections.cshtml` for Manual project documents only
- [ ] `UnsupportedFileTypeException` handled gracefully in POST action
- [ ] No inline styles introduced in any modified view
- [ ] CSS version token bumped if any CSS was added
- [ ] TASKS.md Phase 6 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-1--phase-6-manual-upload-ui`

## Do NOT implement in this phase

- Chapter/folder creation UI for Manual projects — deferred to Tree Builder (V-Sprint 10)
- `.docx` upload support — deferred post V-Sprint 1
- Drag-and-drop upload — deferred
- Upload progress indicator — deferred
- Any reader-facing changes
