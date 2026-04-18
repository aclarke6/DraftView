---
mode: agent
description: V-Sprint 7 Phase 2 — Scheduling
---

# V-Sprint 7 / Phase 2 — Scheduling

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 7, 12, 13 and V-Sprint 7
2. Read `REFACTORING.md` in full
3. Read `.github/copilot-instructions.md`
4. Read `DraftView.Domain/Entities/Section.cs` — understand `IsLocked` from Phase 1
5. Read `DraftView.Domain/Interfaces/Services/IVersioningService.cs`
6. Read `DraftView.Application/Services/VersioningService.cs`
7. Read `DraftView.Web/Controllers/AuthorController.cs`
8. Read `DraftView.Web/Views/Author/Publishing.cshtml`
9. Confirm the active branch is `vsprint-7--phase-2-scheduling`
    — if not on this branch, stop and report
10. Run `git status` — confirm the working tree is clean with no uncommitted changes.
    If uncommitted changes exist that are not part of this phase, stop and report.
11. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Goal

Introduce per-chapter publish scheduling. A scheduled chapter shows a suggested
publish date on the Publishing Page. Scheduling suppresses publish suggestions only —
it never blocks the republish action. The author can always publish ahead of schedule.

> Scheduling never blocks republish. The author decides. Always.

---

## Architecture

Scheduling is advisory. It does not gate any domain operation. The architecture
is therefore deliberately lightweight:

- Domain: `Section.SchedulePublish(DateTime)` / `Section.ClearSchedule()` + `ScheduledPublishAt` property
- Application: `IVersioningService.ScheduleChapterAsync` / `ClearScheduleAsync`
- Infrastructure: EF migration adding `ScheduledPublishAt` to `Sections`
- Web: Schedule/Clear actions + scheduled date display on Publishing Page

No background job. No automatic publish. Scheduling is a display hint only.

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

## Deliverable 1 — Domain: `Section` Scheduling

**File:** `DraftView.Domain/Entities/Section.cs`

Add to `Section`:

```csharp
public DateTime? ScheduledPublishAt { get; private set; }

public void SchedulePublish(DateTime scheduledAt)
{
    if (scheduledAt <= DateTime.UtcNow)
        throw new InvariantViolationException("I-SCHED-PAST",
            "Scheduled publish date must be in the future.");
    ScheduledPublishAt = scheduledAt;
}

public void ClearSchedule()
{
    ScheduledPublishAt = null;
}
```

**Domain Tests:**

```
SchedulePublish_SetsScheduledPublishAt
SchedulePublish_WithPastDate_ThrowsInvariantViolation
SchedulePublish_WithNowDate_ThrowsInvariantViolation
ClearSchedule_SetsScheduledPublishAtToNull
ClearSchedule_WhenNotScheduled_DoesNotThrow
```

---

## Deliverable 2 — Infrastructure: EF Migration

**File:** Migration — `AddSectionScheduling`

Add to `Sections` table:
- `ScheduledPublishAt` — nullable DateTime

Run:
```
dotnet ef migrations add AddSectionScheduling --project DraftView.Infrastructure --startup-project DraftView.Web
dotnet ef database update --project DraftView.Infrastructure --startup-project DraftView.Web
```

Verify the generated migration does not drop or recreate the `Sections` table.

---

## Deliverable 3 — Application: `IVersioningService` Extension

**File:** `DraftView.Domain/Interfaces/Services/IVersioningService.cs`

Add:

```csharp
/// <summary>
/// Sets a suggested publish date for a chapter.
/// Scheduling is advisory — it never blocks the republish action.
/// Throws if the section does not exist or is not a Folder.
/// </summary>
Task ScheduleChapterAsync(Guid chapterId, Guid authorId, DateTime scheduledAt,
    CancellationToken ct = default);

/// <summary>
/// Clears the scheduled publish date for a chapter.
/// Throws if the section does not exist or is not a Folder.
/// </summary>
Task ClearScheduleAsync(Guid chapterId, Guid authorId,
    CancellationToken ct = default);
```

---

## Deliverable 4 — Application: Schedule Implementation

**File:** `DraftView.Application/Services/VersioningService.cs`

Implement `ScheduleChapterAsync`:
1. Load section by `chapterId` — throw `EntityNotFoundException` if not found
2. Validate: `NodeType == Folder` — throw `InvariantViolationException("I-SCHED-NOT-CHAPTER", ...)` if not
3. Call `section.SchedulePublish(scheduledAt)`
4. Save via `unitOfWork.SaveChangesAsync(ct)`

Implement `ClearScheduleAsync`:
1. Load section by `chapterId` — throw `EntityNotFoundException` if not found
2. Validate: `NodeType == Folder`
3. Call `section.ClearSchedule()`
4. Save via `unitOfWork.SaveChangesAsync(ct)`

**No lock guard changes required.** Scheduling does not interact with locking.
Republish on a scheduled chapter works normally — scheduling is advisory only.

**Application Tests:**

```
ScheduleChapterAsync_SetsScheduledPublishAt
ScheduleChapterAsync_WithNonFolder_ThrowsInvariantViolation
ScheduleChapterAsync_WithMissingSection_ThrowsEntityNotFoundException
ScheduleChapterAsync_WithPastDate_ThrowsInvariantViolation
ClearScheduleAsync_ClearsScheduledPublishAt
ClearScheduleAsync_WithNonFolder_ThrowsInvariantViolation
```

---

## Deliverable 5 — Web: Schedule/Clear Actions

**File:** `DraftView.Web/Controllers/AuthorController.cs`

Add POST actions:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ScheduleChapter(Guid chapterId, Guid projectId, DateTime scheduledAt)
{
    var (author, error) = await RequireCurrentAuthorAsync();
    if (error is not null || author is null) return error ?? Forbid();

    try
    {
        await versioningService.ScheduleChapterAsync(chapterId, author.Id, scheduledAt.ToUniversalTime());
        TempData["Success"] = "Publish date set.";
    }
    catch (Exception ex)
    {
        TempData["Error"] = ex.Message;
    }

    return Redirect(Url.Action("Publishing", new { projectId }) + "#section-" + chapterId);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ClearChapterSchedule(Guid chapterId, Guid projectId)
{
    var (author, error) = await RequireCurrentAuthorAsync();
    if (error is not null || author is null) return error ?? Forbid();

    try
    {
        await versioningService.ClearScheduleAsync(chapterId, author.Id);
        TempData["Success"] = "Publish date cleared.";
    }
    catch (Exception ex)
    {
        TempData["Error"] = ex.Message;
    }

    return Redirect(Url.Action("Publishing", new { projectId }) + "#section-" + chapterId);
}
```

---

## Deliverable 6 — Web: Publishing Page Scheduling UI

**File:** `DraftView.Web/Views/Author/Publishing.cshtml`

In the `publishing-chapter__header`, after the classification indicator, add:

```razor
@if (chapter.Chapter.ScheduledPublishAt.HasValue)
{
    <span class="schedule-indicator">
        Suggested: @chapter.Chapter.ScheduledPublishAt.Value.ToString("d MMM yyyy")
    </span>
    <form asp-action="ClearChapterSchedule" method="post" style="display:inline">
        @Html.AntiForgeryToken()
        <input type="hidden" name="chapterId" value="@chapter.Chapter.Id" />
        <input type="hidden" name="projectId" value="@Model.Project.Id" />
        <button type="submit" class="btn btn--sm btn--ghost">Clear date</button>
    </form>
}
else
{
    <form asp-action="ScheduleChapter" method="post" style="display:inline">
        @Html.AntiForgeryToken()
        <input type="hidden" name="chapterId" value="@chapter.Chapter.Id" />
        <input type="hidden" name="projectId" value="@Model.Project.Id" />
        <input type="date" name="scheduledAt" class="input input--sm"
               min="@DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd")" />
        <button type="submit" class="btn btn--sm btn--ghost">Set date</button>
    </form>
}
```

**Important:** The Republish button remains visible and enabled regardless of schedule
state. The schedule is a suggestion only — never a gate.

Style leakage audit: confirm no `style=""` attributes introduced beyond the existing
`style="display:inline"` pattern on forms.

---

## Deliverable 7 — CSS

**File:** `DraftView.Web/wwwroot/css/DraftView.Core.css`

Add:

```css
/* Chapter scheduling — Publishing.cshtml */
.schedule-indicator {
    font-size: var(--text-xs);
    color: var(--color-text-muted);
}

.btn--ghost {
    background: transparent;
    border: 1px solid var(--color-border);
    color: var(--color-text-muted);
    padding: var(--space-1) var(--space-2);
    border-radius: var(--radius-sm, 4px);
    font-size: var(--text-xs);
    cursor: pointer;
}

.btn--ghost:hover {
    background: var(--color-surface-hover, #f3f4f6);
    color: var(--color-text);
}

.input--sm {
    padding: var(--space-1) var(--space-2);
    font-size: var(--text-sm);
    border: 1px solid var(--color-border);
    border-radius: var(--radius-sm, 4px);
}
```

Check if `.btn--ghost` or `.input--sm` already exist in `DraftView.Core.css` before
adding — do not introduce duplicates.

Bump the CSS version token in `DraftView.Core.css` using regex replace.
Update the CSS version in `_Layout.cshtml` to match.

---

## Phase Gate — All Must Pass Before Marking Complete

Run `dotnet test --nologo` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline
- [ ] Solution builds without errors
- [ ] `Section.ScheduledPublishAt` property exists
- [ ] `Section.SchedulePublish()` domain method exists with past-date guard
- [ ] `Section.ClearSchedule()` domain method exists
- [ ] EF migration `AddSectionScheduling` applied cleanly
- [ ] `IVersioningService.ScheduleChapterAsync` exists
- [ ] `IVersioningService.ClearScheduleAsync` exists
- [ ] `AuthorController.ScheduleChapter` POST action exists
- [ ] `AuthorController.ClearChapterSchedule` POST action exists
- [ ] Schedule date shown on Publishing Page when set
- [ ] Clear date button shown when schedule is set
- [ ] Set date form shown when no schedule is set
- [ ] Republish button remains visible and enabled regardless of schedule state
- [ ] CSS scheduling styles added, no duplicates introduced
- [ ] CSS version token bumped
- [ ] No new inline styles introduced
- [ ] Style leakage audit completed on all modified views
- [ ] TASKS.md Phase 2 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-7--phase-2-scheduling`
- [ ] No warnings in test output linked to phase changes
- [ ] Refactor considered and applied where appropriate, tests green after refactor

---

## Identify All Warnings in Tests

Run `dotnet test --nologo` and identify any warnings in the test output.
Address any warnings that are linked to phase changes before proceeding.

---

## Refactor Phase

After implementing the above, consider if any refactor is needed to improve code
quality, as per the refactoring guidelines. In particular:

- `ScheduleChapterAsync` and `LockChapterAsync` share the same chapter-load-and-validate
  pattern — consider whether a private `LoadAndValidateChapterAsync` helper would reduce
  duplication in `VersioningService`.
- Ensure all methods remain under the 30-line threshold per REFACTORING.md section 2.

---

## Do NOT implement in this phase

- Automatic publish when schedule date is reached — out of scope for this sprint
- Per-document scheduling — chapter-level only
- Schedule notifications to readers
- Any changes to the lock mechanism from Phase 1
- Any reader-facing changes
- Any changes to the diff engine or classification
