---
mode: agent
description: V-Sprint 5 Phase 3 — Reader Banner Summary
---

# V-Sprint 5 / Phase 3 — Reader Banner Summary

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 9.2 and V-Sprint 5 Phase 3
2. Read `REFACTORING.md` in full
3. Read `.github/copilot-instructions.md`
4. Read `DraftView.Web/Models/ReaderViewModels.cs` — understand `SceneWithComments`
5. Read `DraftView.Web/Models/MobileReaderViewModels.cs` — understand `MobileReadViewModel`
6. Read `DraftView.Web/Controllers/ReaderController.cs` — understand `ResolveSceneContentAndDiffAsync`
7. Read `DraftView.Web/Views/Reader/DesktopRead.cshtml` — understand the existing `.update-banner`
8. Read `DraftView.Web/Views/Reader/MobileRead.cshtml` — understand the existing `.update-banner`
9. Confirm the active branch is `vsprint-5--phase-3-reader-banner-summary`
   — if not on this branch, stop and report
10. Run `git status` — confirm the working tree is clean with no uncommitted changes.
    If uncommitted changes exist that are not part of this phase, stop and report.
11. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Goal

Show the `AiSummary` from the current `SectionVersion` inside the update banner.
The banner already exists (V-Sprint 3 Phase 3) and shows the version number.
This phase adds the one-line AI summary text to the banner when one is available.

When no `AiSummary` exists on the current version (null), the banner continues
to show the version number only — no empty space, no placeholder text.

---

## Existing Infrastructure to Leverage

- `.update-banner` already rendered in `DesktopRead.cshtml` and `MobileRead.cshtml`
- `SceneWithComments.ShowUpdateBanner` and `CurrentVersionNumber` already on ViewModel
- `MobileReadViewModel.ShowUpdateBanner` and `CurrentVersionNumber` already on ViewModel
- `ISectionVersionRepository.GetLatestAsync` — already called in `ResolveSceneContentAndDiffAsync`
- `SectionVersion.AiSummary` — already on the entity, set during Republish (Phase 2)
- No new services or repositories needed

---

## TDD

View-only changes do not require TDD. Controller changes that load additional
data do benefit from tests — assess as you go.

---

## Deliverable 1 — ViewModel Extension

**File:** `DraftView.Web/Models/ReaderViewModels.cs`

Add `AiSummary` to `SceneWithComments`:

```csharp
/// <summary>
/// The AI-generated one-line summary from the current SectionVersion.
/// Null when no summary exists for the current version.
/// Shown inside the update banner when ShowUpdateBanner is true.
/// </summary>
public string? AiSummary { get; set; }
```

**File:** `DraftView.Web/Models/MobileReaderViewModels.cs`

Add `AiSummary` to `MobileReadViewModel`:

```csharp
/// <summary>
/// The AI-generated one-line summary from the current SectionVersion.
/// Null when no summary exists. Shown in the update banner.
/// </summary>
public string? AiSummary { get; set; }
```

---

## Deliverable 2 — Controller: Populate `AiSummary`

**File:** `DraftView.Web/Controllers/ReaderController.cs`

In `ResolveSceneContentAndDiffAsync`, `latestVersion` is already loaded.
Extract the `AiSummary` from it:

```csharp
var aiSummary = latestVersion?.AiSummary;
```

Extend the return tuple to include `string? aiSummary`.

Update `BuildSceneWithCommentsAsync` and `MobileRead` to consume the new value
and set `SceneWithComments.AiSummary` / `MobileReadViewModel.AiSummary`.

Read the current return tuple signature carefully before modifying.
The tuple is already carrying multiple values — extend it cleanly.

---

## Deliverable 3 — View: Render Summary in Banner

**File:** `DraftView.Web/Views/Reader/DesktopRead.cshtml`

Find the existing `.update-banner` block. The current content is:

```razor
<span class="update-banner__text">
    This section has been updated — version @item.CurrentVersionNumber
</span>
```

Replace with:

```razor
<div class="update-banner__content">
    <span class="update-banner__version">
        Updated — version @item.CurrentVersionNumber
    </span>
    @if (!string.IsNullOrWhiteSpace(item.AiSummary))
    {
        <span class="update-banner__summary">@item.AiSummary</span>
    }
</div>
```

**File:** `DraftView.Web/Views/Reader/MobileRead.cshtml`

Apply the same replacement to the mobile banner:

```razor
<div class="update-banner__content">
    <span class="update-banner__version">
        Updated — version @Model.CurrentVersionNumber
    </span>
    @if (!string.IsNullOrWhiteSpace(Model.AiSummary))
    {
        <span class="update-banner__summary">@Model.AiSummary</span>
    }
</div>
```

Style leakage audit: confirm no `style=""` attributes introduced in either view.

---

## Deliverable 4 — CSS

**File:** `DraftView.Web/wwwroot/css/DraftView.DesktopReader.css`

Extend the existing `.update-banner` CSS block. Add new classes with a comment:

```css
/* Update banner summary — added V-Sprint 5 Phase 3 */
.update-banner__content {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    flex: 1;
}

.update-banner__version {
    color: var(--color-info-text, #1d4ed8);
    font-size: var(--text-sm);
    font-weight: 500;
}

.update-banner__summary {
    color: var(--color-text, #111827);
    font-size: var(--text-sm);
    font-style: italic;
}
```

Do not remove or replace the existing `.update-banner__text` rule — keep it for
backward compatibility in case any existing rendered banners reference it.
Add the new classes alongside the existing ones.

Check `_Layout.cshtml` to confirm `DraftView.DesktopReader.css` is loaded for both
desktop and mobile reader views. If not, move the new classes to `DraftView.Core.css`.

Bump the CSS version token in `DraftView.Core.css` using regex replace.
Update the CSS version in `_Layout.cshtml` to match.

---

## Phase Gate — All Must Pass Before Marking Complete

Run `dotnet test --nologo` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline
- [ ] Solution builds without errors
- [ ] `SceneWithComments.AiSummary` property exists
- [ ] `MobileReadViewModel.AiSummary` property exists
- [ ] `ResolveSceneContentAndDiffAsync` returns `aiSummary` from `latestVersion`
- [ ] `DesktopRead.cshtml` renders `.update-banner__summary` when `AiSummary` is non-null
- [ ] `MobileRead.cshtml` renders `.update-banner__summary` when `AiSummary` is non-null
- [ ] Banner renders version-only when `AiSummary` is null — no empty space or placeholder
- [ ] `.update-banner__content`, `.update-banner__version`, `.update-banner__summary` CSS classes added
- [ ] Existing `.update-banner__text` CSS rule preserved
- [ ] CSS version token bumped
- [ ] No inline styles introduced in any view
- [ ] Style leakage audit completed on both modified views
- [ ] TASKS.md Phase 3 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-5--phase-3-reader-banner-summary`
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

- Editable summary textarea on Republish — deferred to a future sprint
- Author-facing summary display — not in this sprint
- Summary on author dashboard notifications — future
- Any changes to `VersioningService`
- Any changes to `IAiSummaryService`
