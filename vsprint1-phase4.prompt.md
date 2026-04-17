---
mode: agent
description: V-Sprint 1 Phase 4 — Reader Content Source
---

# V-Sprint 1 / Phase 4 — Reader Content Source

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 5 and 9, and V-Sprint 1 Phase 4
2. Read `REFACTORING.md` sections 2, 5, 6, and 9
3. Read `.github/copilot-instructions.md`
4. Read `.github/instructions/versioning.instructions.md`
5. Read `DraftView.Web/Controllers/ReaderController.cs` — understand `DesktopRead` and `MobileRead`
6. Read `DraftView.Web/Models/ReaderViewModels.cs` — understand `SceneWithComments` and `MobileReadViewModel`
7. Confirm the active branch is `vsprint-1--phase-4-reader-content-source`
   — if not on this branch, stop and report
8. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Goal

Readers see `SectionVersion.HtmlContent` instead of `Section.HtmlContent`.
The fallback to `Section.HtmlContent` covers pre-versioning published sections.
`ReadEvent.LastReadVersionNumber` is set when a reader opens a section that has a version.
`Comment.SectionVersionId` is set at comment creation time.

No new UI elements. No new controller actions. Existing readers see identical content.

---

## TDD Sequence

Controller changes that involve data resolution logic benefit from TDD.
Write tests for the content resolution path before implementing it.
View-only changes (swapping `item.Scene.HtmlContent` in Razor) do not require TDD.

---

## Existing Patterns — Follow These Exactly

- All IDs are `Guid`
- `ISectionVersionRepository.GetLatestAsync(sectionId, ct)` returns `SectionVersion?`
- `ReadEvent.UpdateLastReadVersion(int versionNumber)` is the domain method — use it
- `ReadingProgressService.RecordOpenAsync` already handles `ReadEvent` creation/update
  — check whether it needs extending or whether `LastReadVersionNumber` is set separately
- `Comment.CreateRoot`, `Comment.CreateReply`, `Comment.CreateForImport` all accept
  `Guid? sectionVersionId = null` — this was added in Phase 1
- `CommentService.CreateRootCommentAsync` and `CreateReplyAsync` need to pass the current
  `SectionVersionId` — check these service methods and update them
- View changes: `item.Scene.HtmlContent` in `DesktopRead.cshtml` and
  `Model.Scene.HtmlContent` in `MobileRead.cshtml` are the two render points

---

## Deliverable 1 — ViewModel Extension

**File:** `DraftView.Web/Models/ReaderViewModels.cs`

Add `ResolvedHtmlContent` to `SceneWithComments`:

```csharp
/// <summary>
/// The HTML content to render for this scene. Resolved from the latest
/// SectionVersion if one exists, falling back to Section.HtmlContent
/// for pre-versioning published sections.
/// </summary>
public string? ResolvedHtmlContent { get; set; }
```

Add `ResolvedHtmlContent` and `CurrentVersionNumber` to `MobileReadViewModel`:

```csharp
/// <summary>
/// The HTML content to render. Latest SectionVersion if exists,
/// fallback to Scene.HtmlContent for pre-versioning sections.
/// </summary>
public string? ResolvedHtmlContent { get; set; }

/// <summary>
/// The VersionNumber of the SectionVersion used to resolve content.
/// Null if no version exists yet (pre-versioning section).
/// </summary>
public int? CurrentVersionNumber { get; set; }
```

No TDD required for ViewModel property additions.

---

## Deliverable 2 — Controller: Resolve Content from SectionVersion

**File:** `DraftView.Web/Controllers/ReaderController.cs`

`ReaderController` must receive `ISectionVersionRepository` via constructor injection.
Add it to the constructor parameters.

### `DesktopRead` private method

In the `DesktopRead` private method, resolve content for each scene after loading it:

**Resolution logic (apply per scene):**
```
var latestVersion = await sectionVersionRepository.GetLatestAsync(scene.Id, ct);
resolvedHtml = latestVersion?.HtmlContent ?? scene.HtmlContent;
```

Set `SceneWithComments.ResolvedHtmlContent` from the resolved value.

Also update `ReadEvent.LastReadVersionNumber`:
After `await ProgressService.RecordOpenAsync(scene.Id, user.Id)`, if a version exists,
call `UpdateLastReadVersionAsync` on `IReadingProgressService` (see Deliverable 3).

### `MobileRead` private method

Apply the same resolution logic for the single scene.
Set `MobileReadViewModel.ResolvedHtmlContent` and `MobileReadViewModel.CurrentVersionNumber`.

---

## Deliverable 3 — ReadingProgressService Extension

Check `DraftView.Application/Services/ReadingProgressService.cs` and
`DraftView.Domain/Interfaces/Services/IReadingProgressService.cs`.

If no method exists to update `LastReadVersionNumber` on a `ReadEvent`, add:

**Interface addition:**
```csharp
/// <summary>
/// Updates the LastReadVersionNumber on an existing ReadEvent.
/// Called when a reader opens a section that has a current SectionVersion.
/// Does nothing if no ReadEvent exists for the pair.
/// </summary>
Task UpdateLastReadVersionAsync(
    Guid sectionId,
    Guid userId,
    int versionNumber,
    CancellationToken ct = default);
```

**Implementation:** load the `ReadEvent` for `(sectionId, userId)`, call
`readEvent.UpdateLastReadVersion(versionNumber)`, save.

TDD required if this method is added — write failing tests first:
```
UpdateLastReadVersionAsync_UpdatesVersionNumber_WhenReadEventExists
UpdateLastReadVersionAsync_DoesNotThrow_WhenNoReadEventExists
```

---

## Deliverable 4 — CommentService: Anchor to Current Version

**File:** `DraftView.Application/Services/CommentService.cs`

`CreateRootCommentAsync` and `CreateReplyAsync` must pass the current `SectionVersionId`
to the `Comment` factory methods.

Update them to:
1. Call `ISectionVersionRepository.GetLatestAsync(sectionId, ct)`
2. Pass `latestVersion?.Id` as `sectionVersionId` to `Comment.CreateRoot` or `Comment.CreateReply`

`CommentService` must receive `ISectionVersionRepository` via constructor injection.

TDD: add tests covering:
```
CreateRootCommentAsync_SetsCurrentSectionVersionId_WhenVersionExists
CreateRootCommentAsync_SetsNullSectionVersionId_WhenNoVersionExists
```

---

## Deliverable 5 — View Changes

**File:** `DraftView.Web/Views/Reader/DesktopRead.cshtml`

Find the scene prose render line:
```
@Html.Raw(item.Scene.HtmlContent ?? "<p><em>The author has not added content to this scene yet.</em></p>")
```

Replace with:
```
@Html.Raw(item.ResolvedHtmlContent ?? "<p><em>The author has not added content to this scene yet.</em></p>")
```

**File:** `DraftView.Web/Views/Reader/MobileRead.cshtml`

Find:
```
@Html.Raw(Model.Scene.HtmlContent ?? "<p><em>The author has not added content to this scene yet.</em></p>")
```

Replace with:
```
@Html.Raw(Model.ResolvedHtmlContent ?? "<p><em>The author has not added content to this scene yet.</em></p>")
```

No other view changes. No inline styles. No new CSS classes.
Audit both views for style leakage after editing — report any `style=""` attributes found.

---

## Deliverable 6 — DI Verification

Verify `ISectionVersionRepository` is registered in `ServiceCollectionExtensions.cs`
— it was added in Phase 1. Do not add a duplicate.

If `UpdateLastReadVersionAsync` was added, verify `ReadingProgressService` implements it.
DI registration for `ReadingProgressService` is unchanged.

---

## Phase Gate — All Must Pass Before Marking Complete

Run `dotnet test --nologo` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline
- [ ] Solution builds without errors
- [ ] `SceneWithComments.ResolvedHtmlContent` property exists
- [ ] `MobileReadViewModel.ResolvedHtmlContent` property exists
- [ ] `DesktopRead.cshtml` renders `item.ResolvedHtmlContent` not `item.Scene.HtmlContent`
- [ ] `MobileRead.cshtml` renders `Model.ResolvedHtmlContent` not `Model.Scene.HtmlContent`
- [ ] `ReaderController` injects `ISectionVersionRepository`
- [ ] `CommentService` sets `SectionVersionId` on new comments
- [ ] Fallback to `Section.HtmlContent` when no version exists — never null-crash
- [ ] `ReadEvent.LastReadVersionNumber` updated when version exists
- [ ] No new controller actions added
- [ ] No new routes added
- [ ] No inline styles introduced in any view
- [ ] TASKS.md Phase 4 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-1--phase-4-reader-content-source`

## Do NOT implement in this phase

- Version number display in reader UI — deferred
- Update banner or messaging — V-Sprint 3
- Republish button — Phase 5
- Any author-facing UI changes — Phase 5
