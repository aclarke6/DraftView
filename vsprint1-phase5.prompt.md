---
mode: agent
description: V-Sprint 1 Phase 5 — Author Republish UI
---

# V-Sprint 1 / Phase 5 — Author Republish UI

## Agent Instructions

Use **GPT-5.4 mini** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 6 and V-Sprint 1 Phase 5
2. Read `.github/copilot-instructions.md`
3. Read `DraftView.Web/Controllers/AuthorController.cs` — understand `PublishChapter`,
   `UnpublishChapter`, and the `Sections` action before adding `RepublishChapter`
4. Read `DraftView.Web/Views/Author/Sections.cshtml` — understand the existing publish
   button pattern before adding the Republish button
5. Confirm the active branch is `vsprint-1--phase-5-republish-ui`
   — if not on this branch, stop and report
6. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Goal

Add a **Republish** button to the author Sections view for chapters that are already
published and have content that has changed since the last publish.
Clicking Republish calls `IVersioningService.RepublishChapterAsync` and redirects back
to the Sections view.

No new pages. No new routes beyond the single POST action. Minimal view change.

---

## TDD

Controller action changes that delegate to application services benefit from TDD.
The `RepublishChapter` action is a thin controller — it calls `IVersioningService`,
handles exceptions, sets `TempData`, and redirects. This is the same shape as the
existing `PublishChapter` action. TDD is recommended but the test is simple.

View-only changes (adding the Republish button in Razor) do not require TDD.

---

## Existing Patterns — Follow These Exactly

- `AuthorController` uses `[HttpPost, ValidateAntiForgeryToken]` for all mutations
- All POST actions call `RequireCurrentAuthorAsync()` and return early on error
- `TempData["Success"]` and `TempData["Error"]` for feedback — already wired in the view
- Redirect back to `Sections` with `#section-{chapterId}` anchor — same as `PublishChapter`
- `IVersioningService` is already registered in DI (Phase 3)
- `AuthorController` constructor uses primary constructor syntax — add `IVersioningService`
  as a new parameter
- No `#pragma warning disable` needed — follow the existing suppression already present
- CSS: any new button class must be added to the appropriate existing stylesheet with a
  comment. No new stylesheet files. No inline styles.

---

## Deliverable 1 — `RepublishChapter` Controller Action

**File:** `DraftView.Web/Controllers/AuthorController.cs`

Add `IVersioningService versioningService` to the constructor parameter list.

Add the action after `UnpublishChapter`:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RepublishChapter(Guid chapterId, Guid projectId)
{
    var (author, error) = await RequireCurrentAuthorAsync();
    if (error is not null || author is null) return error ?? Forbid();

    try
    {
        await versioningService.RepublishChapterAsync(chapterId, author.Id);
        TempData["Success"] = "Chapter republished. Readers will see the updated content.";
    }
    catch (Exception ex)
    {
        TempData["Error"] = ex.Message;
    }

    return Redirect(Url.Action("Sections", new { projectId }) + "#section-" + chapterId);
}
```

### TDD — `DraftView.Web.Tests`

Check whether a `AuthorControllerTests.cs` exists. If it does, add tests there.
If not, create `DraftView.Web.Tests/Controllers/AuthorControllerRepublishTests.cs`.

Write failing tests before implementing:

```
RepublishChapter_CallsVersioningService_WithCorrectChapterId
RepublishChapter_SetsTempDataSuccess_WhenRepublishSucceeds
RepublishChapter_SetsTempDataError_WhenVersioningServiceThrows
RepublishChapter_RedirectsToSections_AfterRepublish
```

Study the existing `PublishChapter` tests (if any) for the test helper pattern before writing.

---

## Deliverable 2 — Sections View: Republish Button

**File:** `DraftView.Web/Views/Author/Sections.cshtml`

Read the existing file before editing. Find the block that renders the Publish button
for a chapter folder. The Republish button appears alongside the existing Publish/Unpublish
buttons — only when:

- `section.NodeType == NodeType.Folder`
- `section.IsPublished == true`
- `section.ContentChangedSincePublish == true`

The button form follows the same pattern as the existing `PublishChapter` form:

```html
@if (item.Section.NodeType == NodeType.Folder
     && item.Section.IsPublished
     && item.Section.ContentChangedSincePublish)
{
    <form asp-action="RepublishChapter" method="post" style="display:inline">
        @Html.AntiForgeryToken()
        <input type="hidden" name="chapterId" value="@item.Section.Id" />
        <input type="hidden" name="projectId" value="@ViewBag.Project.Id" />
        <button type="submit" class="btn btn--republish">
            Republish
        </button>
    </form>
}
```

**Remove the `style="display:inline"` inline style** — replace it with a CSS class.
Read the existing publish button form to see what CSS approach is already used and follow
the same pattern. Do not introduce inline styles.

### CSS

Add the `.btn--republish` modifier to the appropriate existing stylesheet
(`DraftView.Core.css` or whichever file contains `.btn` variants).
Add a comment indicating it belongs to `Author/Sections.cshtml`.

Bump the CSS version token in `DraftView.Core.css` using regex replace — never hardcode
the expected current version value.

---

## Deliverable 3 — Style Leakage Audit

After editing `Sections.cshtml`, audit the entire file for:
- Any `style=""` attributes
- Any `<style>` blocks

Report findings. Fix any leakage found in the lines you touched.

---

## Phase Gate — All Must Pass Before Marking Complete

Run `dotnet test --nologo` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline
- [ ] Solution builds without errors
- [ ] `AuthorController` constructor includes `IVersioningService`
- [ ] `RepublishChapter` POST action exists and follows existing action shape
- [ ] Republish button only appears when `IsPublished && ContentChangedSincePublish`
- [ ] No inline styles in `Sections.cshtml` on changed lines
- [ ] `.btn--republish` CSS class added to existing stylesheet with comment
- [ ] CSS version token bumped
- [ ] TASKS.md Phase 5 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-1--phase-5-republish-ui`

## Do NOT implement in this phase

- Manual upload UI — Phase 6
- Version history page — deferred
- Notifications on republish — deferred
- Any reader-facing changes — complete in Phase 4
