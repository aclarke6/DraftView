---
mode: agent
description: V-Sprint 9 Phase 3 — Version Management UI
---

# V-Sprint 9 / Phase 3 — Version Management UI

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` V-Sprint 9 Phase 3
2. Read `REFACTORING.md` in full
3. Read `.github/copilot-instructions.md`
4. Read `DraftView.Web/Views/Author/Publishing.cshtml` — understand the current Publishing Page layout
5. Read `DraftView.Web/Controllers/AuthorController.cs` — understand existing publish actions
6. Read `DraftView.Web/Models/AuthorViewModels.cs` — understand existing ViewModels
7. Read `DraftView.Domain/Interfaces/Repositories/ISectionVersionRepository.cs`
8. Read `DraftView.Domain/Policies/VersionRetentionPolicy.cs` — Phase 1 output
9. Confirm the active branch is `vsprint-9--phase-3-version-management-ui`
   — if not on this branch, stop and report
10. Run `git status` — confirm the working tree is clean with no uncommitted changes.
    If uncommitted changes exist that are not part of this phase, stop and report.
11. Run `.\test-summary.ps1` and record the baseline passing count before touching any code

---

## Goal

Add a version history list to the Publishing Page per document, and a controlled
deletion flow that allows the author to remove an older version when the retention
limit has been reached.

Deletion of a version is a permanent, irreversible action. The UI must make this
clear with an explicit confirmation step.

The current latest version can never be deleted via this UI. Only older versions
(not the current reader-visible version) can be deleted.

---

## TDD

Controller actions that load version lists or perform deletion benefit from tests.
View-only rendering does not require TDD. Assess per action.

---

## Deliverable 1 — ViewModel Extension

**File:** `DraftView.Web/Models/AuthorViewModels.cs`

Add `VersionHistoryItem` and extend `PublishingDocumentViewModel`:

```csharp
/// <summary>
/// Represents a single version in the version history list on the Publishing Page.
/// </summary>
public class VersionHistoryItem
{
    public Guid VersionId { get; init; }
    public int VersionNumber { get; init; }
    public DateTime CreatedAt { get; init; }
    public ChangeClassification? Classification { get; init; }
    public bool CanDelete { get; init; }
}
```

Add to `PublishingDocumentViewModel`:

```csharp
/// <summary>
/// Version history for this document. Empty when ShowVersionHistory is false.
/// </summary>
public IReadOnlyList<VersionHistoryItem> VersionHistory { get; init; } = [];

/// <summary>
/// True when the document has reached the retention limit and the version
/// history should be shown to prompt the author to delete an older version.
/// </summary>
public bool ShowVersionHistory { get; init; }

/// <summary>
/// The retention limit for the current tier. Used in the UI prompt.
/// </summary>
public int RetentionLimit { get; init; }
```

---

## Deliverable 2 — Populate Version History in `AuthorController.Publishing`

**File:** `DraftView.Web/Controllers/AuthorController.cs`

In the `Publishing` action, after computing `CanRevoke` for each document,
check if the document is at the retention limit and populate version history:

```csharp
var versionCount = allVersions.Count;
var tier = GetSubscriptionTier();
var limit = VersionRetentionPolicy.GetLimit(tier);
var atLimit = VersionRetentionPolicy.IsAtLimit(versionCount, tier);
var latestVersionNumber = allVersions.Any()
    ? allVersions.Max(v => v.VersionNumber)
    : 0;

var versionHistory = atLimit
    ? allVersions
        .OrderByDescending(v => v.VersionNumber)
        .Select(v => new VersionHistoryItem
        {
            VersionId      = v.Id,
            VersionNumber  = v.VersionNumber,
            CreatedAt      = v.CreatedAt,
            Classification = v.ChangeClassification,
            CanDelete      = v.VersionNumber != latestVersionNumber
        })
        .ToList()
    : [];
```

Add a private `GetSubscriptionTier()` helper to `AuthorController` — same pattern
as `VersioningService`. Read the existing controller constructor before adding
`IConfiguration` if not already injected.

---

## Deliverable 3 — `DeleteVersion` POST Action

**File:** `DraftView.Web/Controllers/AuthorController.cs`

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteVersion(Guid versionId, Guid sectionId, Guid projectId)
{
    var (author, error) = await RequireCurrentAuthorAsync();
    if (error is not null || author is null) return error ?? Forbid();

    try
    {
        // Guard: never allow deleting the latest version via this action
        var allVersions = await sectionVersionRepo.GetAllBySectionIdAsync(sectionId);
        var latest = allVersions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        if (latest is not null && latest.Id == versionId)
        {
            TempData["Error"] = "The current version cannot be deleted. Use Revoke instead.";
            return Redirect(Url.Action("Publishing", new { projectId }) + "#section-" + sectionId);
        }

        await sectionVersionRepo.DeleteAsync(versionId);
        await GetUnitOfWork().SaveChangesAsync();
        TempData["Success"] = "Version deleted.";
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "DeleteVersion failed for version {VersionId}", versionId);
        TempData["Error"] = "Unable to delete version. Please try again.";
    }

    return Redirect(Url.Action("Publishing", new { projectId }) + "#section-" + sectionId);
}
```

---

## Deliverable 4 — View: Version History on Publishing Page

**File:** `DraftView.Web/Views/Author/Publishing.cshtml`

Inside the per-document section, after the existing action buttons, add the
version history when `doc.ShowVersionHistory` is true:

```razor
@if (doc.ShowVersionHistory)
{
    <div class="version-history">
        <p class="version-history__prompt">
            Version limit of @doc.RetentionLimit reached.
            Delete an older version to publish again.
        </p>
        <ul class="version-history__list">
            @foreach (var v in doc.VersionHistory)
            {
                <li class="version-history__item">
                    <span class="version-history__number">v@v.VersionNumber</span>
                    <span class="version-history__date">@v.CreatedAt.ToString("d MMM yyyy")</span>
                    @if (v.Classification.HasValue)
                    {
                        <span class="change-indicator change-indicator--@v.Classification.Value.ToString().ToLower()">
                            @v.Classification.Value
                        </span>
                    }
                    @if (v.CanDelete)
                    {
                        <form asp-action="DeleteVersion" method="post" style="display:inline"
                              onsubmit="return confirm('Permanently delete version @v.VersionNumber? This cannot be undone.');">
                            @Html.AntiForgeryToken()
                            <input type="hidden" name="versionId" value="@v.VersionId" />
                            <input type="hidden" name="sectionId" value="@doc.Document.Id" />
                            <input type="hidden" name="projectId" value="@Model.Project.Id" />
                            <button type="submit" class="btn btn--danger btn--sm">Delete</button>
                        </form>
                    }
                    else
                    {
                        <span class="version-history__current">Current</span>
                    }
                </li>
            }
        </ul>
    </div>
}
```

Style leakage audit: confirm no `style=""` attributes other than the existing
`display:inline` form pattern.

---

## Deliverable 5 — CSS

**File:** `DraftView.Web/wwwroot/css/DraftView.Core.css`

```css
/* Version history — Author/Publishing.cshtml (V-Sprint 9 Phase 3) */
.version-history {
    margin-top: var(--space-3);
    padding: var(--space-3);
    background: var(--color-surface-subtle, #fafafa);
    border: 1px solid var(--color-border);
    border-radius: var(--radius-sm, 4px);
}

.version-history__prompt {
    font-size: var(--text-sm);
    color: var(--color-danger, #b91c1c);
    margin-bottom: var(--space-2);
}

.version-history__list {
    list-style: none;
    padding: 0;
    margin: 0;
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
}

.version-history__item {
    display: flex;
    align-items: center;
    gap: var(--space-3);
}

.version-history__number {
    font-weight: 600;
    font-size: var(--text-sm);
    min-width: 2rem;
}

.version-history__date {
    font-size: var(--text-xs);
    color: var(--color-text-muted);
}

.version-history__current {
    font-size: var(--text-xs);
    color: var(--color-text-muted);
    font-style: italic;
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
- [ ] `VersionHistoryItem` ViewModel exists
- [ ] `PublishingDocumentViewModel` has `VersionHistory`, `ShowVersionHistory`, `RetentionLimit`
- [ ] `Publishing` action populates version history when at limit
- [ ] `DeleteVersion` POST action exists with latest-version guard
- [ ] `Publishing.cshtml` renders version history when `ShowVersionHistory` is true
- [ ] Delete button shows browser confirmation dialog
- [ ] Current version shown as "Current" — not deletable via this UI
- [ ] CSS classes added for version history layout
- [ ] CSS version token bumped
- [ ] No new inline styles in views
- [ ] Style leakage audit completed on `Publishing.cshtml`
- [ ] TASKS.md Phase 3 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-9--phase-3-version-management-ui`
- [ ] No warnings in test output linked to phase changes
- [ ] Refactor considered and applied where appropriate, tests green after refactor

---

## Do NOT implement in this phase

- Billing or subscription tier persistence
- Automatic pruning of old versions
- Version comparison UI
- Any reader-facing changes
