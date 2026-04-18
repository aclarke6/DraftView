---
mode: agent
description: V-Sprint 3 Phase 3 — Update Banner
---

# V-Sprint 3 / Phase 3 — Update Banner

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 9.2 and V-Sprint 3 Phase 3
2. Read `REFACTORING.md` in full
3. Read `.github/copilot-instructions.md`
4. Read `DraftView.Domain/Entities/ReadEvent.cs` — understand the entity after Phase 1
5. Read `DraftView.Web/Controllers/ReaderController.cs` — understand current structure
6. Read `DraftView.Web/Models/ReaderViewModels.cs`
7. Read `DraftView.Web/Models/MobileReaderViewModels.cs`
8. Read `DraftView.Web/Views/Reader/DesktopRead.cshtml`
9. Read `DraftView.Web/Views/Reader/MobileRead.cshtml`
10. Confirm the active branch is `vsprint-3--phase-3-update-banner`
    — if not on this branch, stop and report
11. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Goal

Show a non-blocking top banner in reader views when the reader has previously read
a section and a newer version now exists. The banner is dismissible, shown once per
version per reader, and shows the current version number.

Dismissal is persisted so the banner does not reappear when the reader returns to the
same section on the same version. Dismissal is stored on `ReadEvent` as
`BannerDismissedAtVersion` — a nullable int. When this equals the current version
number, the banner is not shown.

AI summary text is deferred to V-Sprint 5 — the banner shows version number only.

---

## Existing Infrastructure to Leverage

- `ReadEvent` — already carries `LastReadVersionNumber` and `LastReadAt` (Phase 1)
- `ISectionVersionRepository.GetLatestAsync` — already in controller
- `IReadEventRepository.GetAsync` — already in controller
- `SectionDiffResult.HasChanges` and `CurrentVersionNumber` — already computed
- `UpdatedSinceLastRead` — already on ViewModels (Phase 2)
- No new application service needed — dismissal is a thin controller action

---

## TDD Sequence — Mandatory for Domain Changes

Domain changes require TDD. Controller and view changes are assessed case by case.

---

## Deliverable 1 — `ReadEvent.BannerDismissedAtVersion`

**File:** `DraftView.Domain/Entities/ReadEvent.cs`

Add to the Properties region:

```csharp
/// <summary>
/// The version number at which the reader dismissed the update banner.
/// When this equals the current version number, the banner is not shown.
/// Null until the reader has dismissed the banner for the first time.
/// </summary>
public int? BannerDismissedAtVersion { get; private set; }
```

Add to the Behaviour region:

```csharp
/// <summary>
/// Records that the reader dismissed the update banner at the given version.
/// Subsequent opens of the same version will not show the banner.
/// </summary>
/// <param name="versionNumber">The version number being dismissed (must be >= 1).</param>
/// <exception cref="InvariantViolationException">Thrown when version number is less than 1.</exception>
public void DismissBannerAtVersion(int versionNumber)
{
    if (versionNumber < 1)
        throw new InvariantViolationException("I-READ-BANNER",
            "Version number must be 1 or greater.");

    BannerDismissedAtVersion = versionNumber;
}
```

### Domain Tests

**File:** `DraftView.Domain.Tests/Entities/ReadEventTests.cs`

Add to the existing test class. Write all tests **failing** before implementing:

```
DismissBannerAtVersion_SetsBannerDismissedAtVersion
DismissBannerAtVersion_OverwritesPreviousValue
DismissBannerAtVersion_WithVersionLessThanOne_ThrowsInvariantViolation
Create_HasNullBannerDismissedAtVersion
```

Run full test suite. Zero regressions.
Commit: `domain: add BannerDismissedAtVersion and DismissBannerAtVersion to ReadEvent`

---

## Deliverable 2 — EF Configuration

**File:** `DraftView.Infrastructure/Persistence/Configurations/ReadEventConfiguration.cs`

Add mapping for `BannerDismissedAtVersion`:

```csharp
builder.Property(e => e.BannerDismissedAtVersion)
    .HasColumnName("BannerDismissedAtVersion")
    .IsRequired(false);
```

---

## Deliverable 3 — EF Migration

Run:

```
dotnet ef migrations add AddBannerDismissedAtVersionToReadEvents --project DraftView.Infrastructure --startup-project DraftView.Web
```

Review the migration — confirm it adds a nullable int column `BannerDismissedAtVersion`
to `ReadEvents`. Must not alter any existing columns.

Apply:

```
dotnet ef database update --project DraftView.Infrastructure --startup-project DraftView.Web
```

---

## Deliverable 4 — ViewModel Extension

**File:** `DraftView.Web/Models/ReaderViewModels.cs`

Add to `SceneWithComments`:

```csharp
/// <summary>
/// True when the update banner should be shown for this scene.
/// Requires: reader has read before, a newer version exists, and
/// the reader has not yet dismissed the banner at the current version.
/// </summary>
public bool ShowUpdateBanner { get; set; }

/// <summary>
/// The current version number. Used in the banner label.
/// </summary>
public int? CurrentVersionNumber { get; set; }
```

**File:** `DraftView.Web/Models/MobileReaderViewModels.cs`

Add to `MobileReadViewModel`:

```csharp
/// <summary>True when the update banner should be shown.</summary>
public bool ShowUpdateBanner { get; set; }
```

`CurrentVersionNumber` is already on `MobileReadViewModel` from V-Sprint 1 — do not add a duplicate.

---

## Deliverable 5 — Controller: Populate Banner State

**File:** `DraftView.Web/Controllers/ReaderController.cs`

Extend `ResolveSceneContentAndDiffAsync` to return `showUpdateBanner`.

The banner is shown when all three conditions are true:
1. `diffResult is not null && diffResult.HasChanges` — newer version exists
2. `readEvent?.LastReadVersionNumber is not null` — reader has read before
3. `readEvent?.BannerDismissedAtVersion != diffResult.CurrentVersionNumber` — not yet dismissed

```csharp
var showUpdateBanner = diffResult is not null
    && diffResult.HasChanges
    && readEvent?.LastReadVersionNumber is not null
    && readEvent?.BannerDismissedAtVersion != diffResult.CurrentVersionNumber;
```

Populate `SceneWithComments.ShowUpdateBanner`, `SceneWithComments.CurrentVersionNumber`,
and `MobileReadViewModel.ShowUpdateBanner` from these values.

---

## Deliverable 6 — Dismiss Banner Action

**File:** `DraftView.Web/Controllers/ReaderController.cs`

Add a POST action to record banner dismissal:

```csharp
// -----------------------------------------------------------------------
// POST: /Reader/DismissBanner
// -----------------------------------------------------------------------
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DismissBanner(Guid sectionId, int versionNumber)
{
    var user = await GetCurrentUserAsync();
    if (user is null)
        return Forbid();

    await ProgressService.DismissBannerAsync(sectionId, user.Id, versionNumber);
    return Ok();
}
```

---

## Deliverable 7 — `IReadingProgressService.DismissBannerAsync`

**File:** `DraftView.Domain/Interfaces/Services/IReadingProgressService.cs`

Add:

```csharp
/// <summary>
/// Records that the reader dismissed the update banner for a section at a
/// specific version. Subsequent views of the same version will not show the banner.
/// No-op if no ReadEvent exists.
/// </summary>
Task DismissBannerAsync(Guid sectionId, Guid userId, int versionNumber, CancellationToken ct = default);
```

**File:** `DraftView.Application/Services/ReadingProgressService.cs`

Implement:

```csharp
public async Task DismissBannerAsync(Guid sectionId, Guid userId, int versionNumber, CancellationToken ct = default)
{
    var readEvent = await _readEventRepo.GetAsync(sectionId, userId, ct);
    if (readEvent is null) return;

    readEvent.DismissBannerAtVersion(versionNumber);
    await _unitOfWork.SaveChangesAsync(ct);
}
```

### Tests

**File:** `DraftView.Application.Tests/Services/ReadingProgressServiceTests.cs`

Add to the existing test class:

```
DismissBannerAsync_SetsBannerDismissedAtVersion_WhenReadEventExists
DismissBannerAsync_DoesNotThrow_WhenNoReadEventExists
```

Run full test suite. Zero regressions.
Commit: `app: add DismissBannerAsync to ReadingProgressService`

---

## Deliverable 8 — View: Render Banner

**File:** `DraftView.Web/Views/Reader/DesktopRead.cshtml`

Add the banner inside the scene loop, above the scene title or immediately below it:

```razor
@if (item.ShowUpdateBanner)
{
    <div class="update-banner" data-section-id="@item.Scene.Id" data-version="@item.CurrentVersionNumber">
        <span class="update-banner__text">
            This section has been updated — version @item.CurrentVersionNumber
        </span>
        <button type="button" class="update-banner__dismiss" aria-label="Dismiss update notice">
            Dismiss
        </button>
    </div>
}
```

**File:** `DraftView.Web/Views/Reader/MobileRead.cshtml`

Add the banner above the prose block:

```razor
@if (Model.ShowUpdateBanner)
{
    <div class="update-banner" data-section-id="@Model.Scene.Id" data-version="@Model.CurrentVersionNumber">
        <span class="update-banner__text">
            This section has been updated — version @Model.CurrentVersionNumber
        </span>
        <button type="button" class="update-banner__dismiss" aria-label="Dismiss update notice">
            Dismiss
        </button>
    </div>
}
```

Style leakage audit: confirm no `style=""` attributes introduced in any modified view.

---

## Deliverable 9 — Dismiss JavaScript

In both `DesktopRead.cshtml` and `MobileRead.cshtml`, add to the existing scripts section:

```javascript
document.querySelectorAll('.update-banner__dismiss').forEach(function(btn) {
    btn.addEventListener('click', function() {
        var banner = btn.closest('.update-banner');
        var sectionId = banner.dataset.sectionId;
        var version = banner.dataset.version;

        fetch('/Reader/DismissBanner', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': document.querySelector(
                    'input[name="__RequestVerificationToken"]').value
            },
            body: 'sectionId=' + sectionId + '&versionNumber=' + version
        });

        banner.remove();
    });
});
```

The POST is fire-and-forget — banner removed from DOM immediately on click regardless
of server response. Ensure an antiforgery token input is available on the page —
check if `@Html.AntiForgeryToken()` is already present before adding it.

---

## Deliverable 10 — CSS

**File:** `DraftView.Web/wwwroot/css/DraftView.DesktopReader.css`

Add banner styles with a comment indicating which views use them:

```css
/* Update banner — DesktopRead.cshtml / MobileRead.cshtml */
.update-banner {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-3);
    background: var(--color-info-bg, #eff6ff);
    border: 1px solid var(--color-info-border, #bfdbfe);
    border-radius: var(--radius-sm, 4px);
    padding: var(--space-2) var(--space-4);
    margin-bottom: var(--space-4);
    font-family: var(--font-ui);
    font-size: var(--text-sm);
}

.update-banner__text {
    color: var(--color-info-text, #1d4ed8);
    flex: 1;
}

.update-banner__dismiss {
    background: none;
    border: none;
    color: var(--color-text-muted);
    cursor: pointer;
    font-size: var(--text-sm);
    padding: 0;
    white-space: nowrap;
}

.update-banner__dismiss:hover {
    color: var(--color-text);
}
```

Check `_Layout.cshtml` to confirm the stylesheet is loaded on both desktop and mobile
reader views. If not, move the banner styles to `DraftView.Core.css`.

Bump the CSS version token in `DraftView.Core.css` using regex replace.
Update the CSS version in `_Layout.cshtml` to match.

---

## Phase Gate — All Must Pass Before Marking Complete

Run `dotnet test --nologo` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline
- [ ] Solution builds without errors
- [ ] `ReadEvent.BannerDismissedAtVersion` property exists
- [ ] `ReadEvent.DismissBannerAtVersion` method exists
- [ ] EF migration created and applied
- [ ] `IReadingProgressService.DismissBannerAsync` exists
- [ ] `ReadingProgressService.DismissBannerAsync` implemented
- [ ] `SceneWithComments.ShowUpdateBanner` and `CurrentVersionNumber` exist
- [ ] `MobileReadViewModel.ShowUpdateBanner` exists
- [ ] `ReaderController.DismissBanner` POST action exists
- [ ] `DesktopRead.cshtml` renders `.update-banner` when `ShowUpdateBanner` is true
- [ ] `MobileRead.cshtml` renders `.update-banner` when `ShowUpdateBanner` is true
- [ ] Dismiss button removes banner from DOM via JavaScript
- [ ] Dismiss POST fires to `/Reader/DismissBanner`
- [ ] `.update-banner` CSS classes added to stylesheet
- [ ] CSS version token bumped
- [ ] No inline styles introduced in any view
- [ ] Style leakage audit completed on all modified views
- [ ] TASKS.md Phase 3 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-3--phase-3-update-banner`
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

- AI summary in banner — V-Sprint 5
- Version history navigation — V-Sprint 9
- Change classification label — V-Sprint 4
- Any author-facing changes
