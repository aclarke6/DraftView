---
mode: agent
description: V-Sprint 7 Phase 1 — Chapter Locking
---

# V-Sprint 7 / Phase 1 — Chapter Locking

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 7, 8, 12, 13 and V-Sprint 7
2. Read `REFACTORING.md` in full
3. Read `.github/copilot-instructions.md`
4. Read `DraftView.Domain/Entities/Section.cs` — understand current publish/unpublish domain methods
5. Read `DraftView.Domain/Interfaces/Services/IVersioningService.cs` — understand existing versioning contracts
6. Read `DraftView.Application/Services/VersioningService.cs` — understand `RepublishChapterAsync`
7. Read `DraftView.Web/Controllers/AuthorController.cs` — understand existing publish actions
8. Read `DraftView.Web/Views/Author/Publishing.cshtml` — understand the Publishing Page structure
9. Read `DraftView.Web/Views/Author/Sections.cshtml` — understand the Sections view structure
10. Confirm the active branch is `vsprint-7--phase-1-chapter-locking`
    — if not on this branch, stop and report
11. Run `git status` — confirm the working tree is clean with no uncommitted changes.
    If uncommitted changes exist that are not part of this phase, stop and report.
12. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Goal

Introduce chapter locking. A locked chapter blocks all publish actions on that chapter
and its documents. Readers see a "Author is revising this chapter" message when a
locked chapter's content is accessed.

Locking is author-controlled. Lock and Unlock are explicit actions. Locking never
occurs automatically.

---

## Architecture

This phase spans Domain → Application → Infrastructure → Web.

- Domain: `Section.Lock()` / `Section.Unlock()` methods + `IsLocked` property
- Application: `IVersioningService.LockChapterAsync` / `UnlockChapterAsync`; lock guard in `RepublishChapterAsync` and `RepublishSectionAsync`
- Infrastructure: EF migration adding `IsLocked` and `LockedAt` to `Sections`
- Web: Lock/Unlock actions on Publishing Page; reader message when locked

---

## TDD Sequence — Mandatory

Search all existing test files before writing any new tests.
Never write a duplicate test.

1. Add domain method stubs with `throw new NotImplementedException()`
2. Write all failing domain tests
3. Confirm tests are red
4. Implement domain to make tests green
5. Write failing application tests
6. Implement application layer to make tests green
7. Run full test suite — zero regressions before committing

---

## Deliverable 1 — Domain: `Section` Locking

**File:** `DraftView.Domain/Entities/Section.cs`

Add to `Section`:

```csharp
public bool IsLocked { get; private set; }
public DateTime? LockedAt { get; private set; }

public void Lock()
{
    if (IsLocked)
        throw new InvariantViolationException("I-LOCK-ALREADY", "Section is already locked.");
    IsLocked = true;
    LockedAt = DateTime.UtcNow;
}

public void Unlock()
{
    if (!IsLocked)
        throw new InvariantViolationException("I-LOCK-NOT-LOCKED", "Section is not locked.");
    IsLocked = false;
    LockedAt = null;
}
```

**Domain Tests:**

```
Lock_SetsIsLockedTrue
Lock_SetsLockedAt
Lock_WhenAlreadyLocked_ThrowsInvariantViolation
Unlock_SetsIsLockedFalse_WhenLocked
Unlock_ClearsLockedAt
Unlock_WhenNotLocked_ThrowsInvariantViolation
```

---

## Deliverable 2 — Infrastructure: EF Migration

**File:** Migration — `AddSectionLocking`

Add to `Sections` table:
- `IsLocked` — bool, not null, default false
- `LockedAt` — nullable DateTime

Run:
```
dotnet ef migrations add AddSectionLocking --project DraftView.Infrastructure --startup-project DraftView.Web
dotnet ef database update --project DraftView.Infrastructure --startup-project DraftView.Web
```

Verify the generated migration does not drop or recreate the `Sections` table.
Add EF mapping for the new columns if not auto-detected.

---

## Deliverable 3 — Application: `IVersioningService` Extension

**File:** `DraftView.Domain/Interfaces/Services/IVersioningService.cs`

Add:

```csharp
/// <summary>
/// Locks a chapter (Folder section), blocking all publish actions.
/// Throws if the section does not exist or is not a Folder.
/// </summary>
Task LockChapterAsync(Guid chapterId, Guid authorId, CancellationToken ct = default);

/// <summary>
/// Unlocks a chapter, re-enabling publish actions.
/// Throws if the section does not exist, is not a Folder, or is not locked.
/// </summary>
Task UnlockChapterAsync(Guid chapterId, Guid authorId, CancellationToken ct = default);
```

---

## Deliverable 4 — Application: Lock/Unlock Implementation

**File:** `DraftView.Application/Services/VersioningService.cs`

Implement `LockChapterAsync`:
1. Load section by `chapterId` — throw `EntityNotFoundException` if not found
2. Validate: `NodeType == Folder` — throw `InvariantViolationException("I-LOCK-NOT-CHAPTER", ...)` if not
3. Call `section.Lock()`
4. Save via `unitOfWork.SaveChangesAsync(ct)`

Implement `UnlockChapterAsync`:
1. Load section by `chapterId` — throw `EntityNotFoundException` if not found
2. Validate: `NodeType == Folder`
3. Call `section.Unlock()`
4. Save via `unitOfWork.SaveChangesAsync(ct)`

**Lock guard — add to `RepublishChapterAsync`:**

After loading the chapter and before creating any versions, add:

```csharp
if (chapter.IsLocked)
    throw new InvariantViolationException("I-LOCK-BLOCKED",
        "Cannot republish a locked chapter. Unlock the chapter first.");
```

**Lock guard — add to `RepublishSectionAsync`:**

After loading the section, resolve its parent chapter and check:

```csharp
var parentChapter = section.ParentId.HasValue
    ? await sectionRepo.GetByIdAsync(section.ParentId.Value, ct)
    : null;

if (parentChapter is { IsLocked: true })
    throw new InvariantViolationException("I-LOCK-BLOCKED",
        "Cannot republish a document in a locked chapter. Unlock the chapter first.");
```

**Application Tests:**

```
LockChapterAsync_SetsIsLocked
LockChapterAsync_WithNonFolder_ThrowsInvariantViolation
LockChapterAsync_WithMissingSection_ThrowsEntityNotFoundException
UnlockChapterAsync_ClearsIsLocked
UnlockChapterAsync_WithNonFolder_ThrowsInvariantViolation
UnlockChapterAsync_WhenNotLocked_ThrowsInvariantViolation
RepublishChapterAsync_WhenLocked_ThrowsInvariantViolation
RepublishSectionAsync_WhenParentChapterLocked_ThrowsInvariantViolation
```

---

## Deliverable 5 — Web: Lock/Unlock Actions

**File:** `DraftView.Web/Controllers/AuthorController.cs`

Add POST actions:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> LockChapter(Guid chapterId, Guid projectId)
{
    var (author, error) = await RequireCurrentAuthorAsync();
    if (error is not null || author is null) return error ?? Forbid();

    try
    {
        await versioningService.LockChapterAsync(chapterId, author.Id);
        TempData["Success"] = "Chapter locked.";
    }
    catch (Exception ex)
    {
        TempData["Error"] = ex.Message;
    }

    return Redirect(Url.Action("Publishing", new { projectId }) + "#section-" + chapterId);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> UnlockChapter(Guid chapterId, Guid projectId)
{
    var (author, error) = await RequireCurrentAuthorAsync();
    if (error is not null || author is null) return error ?? Forbid();

    try
    {
        await versioningService.UnlockChapterAsync(chapterId, author.Id);
        TempData["Success"] = "Chapter unlocked.";
    }
    catch (Exception ex)
    {
        TempData["Error"] = ex.Message;
    }

    return Redirect(Url.Action("Publishing", new { projectId }) + "#section-" + chapterId);
}
```

---

## Deliverable 6 — Web: Publishing Page Lock Controls

**File:** `DraftView.Web/Views/Author/Publishing.cshtml`

In the `publishing-chapter__actions` div, alongside the Republish Chapter button, add:

```razor
@if (chapter.Chapter.IsLocked)
{
    <span class="lock-indicator lock-indicator--locked">Locked</span>
    <form asp-action="UnlockChapter" method="post" style="display:inline">
        @Html.AntiForgeryToken()
        <input type="hidden" name="chapterId" value="@chapter.Chapter.Id" />
        <input type="hidden" name="projectId" value="@Model.Project.Id" />
        <button type="submit" class="btn btn--sm btn--secondary">Unlock</button>
    </form>
}
else
{
    <form asp-action="LockChapter" method="post" style="display:inline">
        @Html.AntiForgeryToken()
        <input type="hidden" name="chapterId" value="@chapter.Chapter.Id" />
        <input type="hidden" name="projectId" value="@Model.Project.Id" />
        <button type="submit" class="btn btn--sm btn--secondary">Lock</button>
    </form>
}
```

When a chapter is locked, the Republish Chapter button must be hidden:

```razor
@if (chapter.HasChanges && !chapter.Chapter.IsLocked)
{
    <form asp-action="RepublishChapter" ...>
```

Style leakage audit: confirm no `style=""` attributes introduced beyond the existing
`style="display:inline"` pattern on forms.

---

## Deliverable 7 — Web: Reader Lock Message

**File:** `DraftView.Web/Views/Reader/DesktopRead.cshtml`

At the top of the scene render block, before prose output, add:

```razor
@if (Model.Chapter.IsLocked)
{
    <div class="lock-notice">
        <p>The author is currently revising this chapter.</p>
    </div>
}
```

Apply the identical change to `DraftView.Web/Views/Reader/MobileRead.cshtml`.

Inspect `DesktopChapterReadViewModel` / `MobileReadViewModel` before adding — add
`IsChapterLocked` bool if the full `Section` object is not already present.

---

## Deliverable 8 — CSS

**File:** `DraftView.Web/wwwroot/css/DraftView.Core.css`

Add:

```css
/* Chapter locking — Publishing.cshtml / DesktopRead.cshtml / MobileRead.cshtml */
.lock-indicator {
    font-size: var(--text-xs);
    font-weight: 600;
    padding: 2px var(--space-2);
    border-radius: var(--radius-sm, 4px);
    text-transform: uppercase;
    letter-spacing: 0.05em;
}

.lock-indicator--locked {
    background: var(--color-warning-bg, #fef3c7);
    color: var(--color-warning-text, #92400e);
}

.lock-notice {
    border-left: 3px solid var(--color-warning, #f59e0b);
    padding: var(--space-3) var(--space-4);
    margin-bottom: var(--space-4);
    background: var(--color-warning-bg, #fef3c7);
    border-radius: 0 var(--radius-md, 6px) var(--radius-md, 6px) 0;
    color: var(--color-warning-text, #92400e);
    font-size: var(--text-sm);
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
- [ ] `Section.IsLocked` and `Section.LockedAt` properties exist
- [ ] `Section.Lock()` and `Section.Unlock()` domain methods exist with invariant guards
- [ ] EF migration `AddSectionLocking` applied cleanly
- [ ] `IVersioningService.LockChapterAsync` and `UnlockChapterAsync` exist
- [ ] Lock guard present in `RepublishChapterAsync` — throws when chapter is locked
- [ ] Lock guard present in `RepublishSectionAsync` — throws when parent chapter is locked
- [ ] `AuthorController.LockChapter` POST action exists
- [ ] `AuthorController.UnlockChapter` POST action exists
- [ ] Lock/Unlock buttons shown on Publishing Page per chapter
- [ ] Republish button hidden on Publishing Page when chapter is locked
- [ ] Reader lock message shown on Desktop and Mobile read views when chapter is locked
- [ ] CSS lock styles added
- [ ] CSS version token bumped
- [ ] No new inline styles introduced
- [ ] Style leakage audit completed on all modified views
- [ ] TASKS.md Phase 1 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-7--phase-1-chapter-locking`
- [ ] No warnings in test output linked to phase changes
- [ ] Refactor considered and applied where appropriate, tests green after refactor

---

## Identify All Warnings in Tests

Run `dotnet test --nologo` and identify any warnings in the test output.
Address any warnings that are linked to code changes made in this phase before
proceeding.

---

## Refactor Phase

After implementing the above, consider if any refactor is needed to improve code
quality, as per the refactoring guidelines. In particular:

- Lock guard logic in `RepublishChapterAsync` and `RepublishSectionAsync` should be
  consistent in shape — consider a private `AssertChapterNotLocked(Section chapter)` helper.
- Ensure all methods remain under the 30-line threshold per REFACTORING.md section 2.

---

## Do NOT implement in this phase

- Scheduling — Phase 2
- Lock expiry or automatic unlock
- Lock history or audit trail
- Any reader changes beyond the lock message
- Any changes to the diff engine or classification
