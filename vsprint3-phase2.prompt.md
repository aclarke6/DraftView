---
mode: agent
description: V-Sprint 3 Phase 2 — Update Messaging
---

# V-Sprint 3 / Phase 2 — Update Messaging

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 9.1 and V-Sprint 3 Phase 2
2. Read `REFACTORING.md` in full
3. Read `.github/copilot-instructions.md`
4. Read `DraftView.Web/Models/ReaderViewModels.cs` — understand `SceneWithComments`
5. Read `DraftView.Web/Models/MobileReaderViewModels.cs` — understand `MobileReadViewModel`
6. Read `DraftView.Web/Controllers/ReaderController.cs` — understand `ResolveSceneContentAndDiffAsync`
7. Read `DraftView.Web/Views/Reader/DesktopRead.cshtml` — understand the scene prose render block
8. Read `DraftView.Web/Views/Reader/MobileRead.cshtml` — understand the prose render block
9. Confirm the active branch is `vsprint-3--phase-2-update-messaging`
   — if not on this branch, stop and report
10. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Goal

Show an "Updated since you last read" inline message per scene when the reader has
previously read this section and a newer version now exists.

The message appears above the prose content — between the scene title and the text.
It is always-on (no dismiss in this phase). It is shown only when both conditions
are true: the reader has read this section before (`LastReadVersionNumber` is set)
AND a newer version exists (`HasChanges == true` from `SectionDiffResult`).

No message is shown on first read or when the reader is on the latest version.

This is distinct from the Update Banner (Phase 3) — this is an inline per-scene
message, not a top-of-page banner.

---

## Existing Infrastructure to Leverage

- `SectionDiffResult.HasChanges` — already computed in `ResolveSceneContentAndDiffAsync`
- `SceneWithComments.HasDiff` — already on ViewModel (V-Sprint 2 Phase 3)
- `ReadEvent.LastReadVersionNumber` — already set on section open (V-Sprint 1 Phase 4)
- `ReadEvent.LastReadAt` — added in Phase 1 of this sprint
- No new services needed

---

## TDD

View-only and thin ViewModel property changes do not require TDD.
Controller changes that involve conditional logic benefit from tests — assess as you go.

---

## Deliverable 1 — ViewModel Extension

**File:** `DraftView.Web/Models/ReaderViewModels.cs`

Add `UpdatedSinceLastRead` to `SceneWithComments`:

```csharp
/// <summary>
/// True when the reader has previously read this section and a newer version
/// now exists. Drives the "Updated since you last read" inline message.
/// False on first read or when the reader is already on the latest version.
/// </summary>
public bool UpdatedSinceLastRead { get; set; }
```

**File:** `DraftView.Web/Models/MobileReaderViewModels.cs`

Add `UpdatedSinceLastRead` to `MobileReadViewModel`:

```csharp
/// <summary>
/// True when the reader has previously read this scene and a newer version exists.
/// </summary>
public bool UpdatedSinceLastRead { get; set; }
```

---

## Deliverable 2 — Controller: Populate `UpdatedSinceLastRead`

**File:** `DraftView.Web/Controllers/ReaderController.cs`

In `ResolveSceneContentAndDiffAsync`, the method already computes `diffResult`.
The condition for showing the message is:

- `diffResult` is not null (a version exists)
- `diffResult.HasChanges` is true (there are changes since last read)
- `readEvent?.LastReadVersionNumber` is not null (reader has read before)

Extend the return tuple to include `bool updatedSinceLastRead`:

```csharp
private async Task<(string? resolvedHtml, int? currentVersionNumber,
    IReadOnlyList<ParagraphDiffResult> diffParagraphs, bool updatedSinceLastRead)>
    ResolveSceneContentAndDiffAsync(Section scene, Guid userId, CancellationToken ct = default)
```

Set `updatedSinceLastRead`:

```csharp
var updatedSinceLastRead = diffResult is not null
    && diffResult.HasChanges
    && readEvent?.LastReadVersionNumber is not null;
```

Update `BuildSceneWithCommentsAsync` and `MobileRead` to consume the new value
and set `SceneWithComments.UpdatedSinceLastRead` / `MobileReadViewModel.UpdatedSinceLastRead`.

---

## Deliverable 3 — View: Render Inline Message

**File:** `DraftView.Web/Views/Reader/DesktopRead.cshtml`

Find the scene content area. Add the inline message between the scene title and the
prose content, immediately before the `scene-prose-content` div:

```razor
@if (item.UpdatedSinceLastRead)
{
    <div class="scene-updated-notice">
        Updated since you last read
    </div>
}
```

**File:** `DraftView.Web/Views/Reader/MobileRead.cshtml`

Add the same message above the mobile prose block:

```razor
@if (Model.UpdatedSinceLastRead)
{
    <div class="scene-updated-notice">
        Updated since you last read
    </div>
}
```

Style leakage audit: after editing both views, confirm no `style=""` attributes
introduced anywhere in the modified views.

---

## Deliverable 4 — CSS

**File:** `DraftView.Web/wwwroot/css/DraftView.DesktopReader.css`

Add the notice class with a comment indicating which views use it:

```css
/* Inline update notice — DesktopRead.cshtml / MobileRead.cshtml */
.scene-updated-notice {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    background: var(--color-info-bg, #eff6ff);
    border: 1px solid var(--color-info-border, #bfdbfe);
    color: var(--color-info-text, #1d4ed8);
    border-radius: var(--radius-sm, 4px);
    padding: var(--space-1) var(--space-3);
    font-size: var(--text-sm);
    font-family: var(--font-ui);
    margin-bottom: var(--space-4);
}
```

Check `_Layout.cshtml` to confirm this stylesheet is loaded on both desktop and
mobile reader views. If not, move the class to `DraftView.Core.css`.

Bump the CSS version token in `DraftView.Core.css` using regex replace.
Update the CSS version in `_Layout.cshtml` to match.

---

## Phase Gate — All Must Pass Before Marking Complete

Run `dotnet test --nologo` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline
- [ ] Solution builds without errors
- [ ] `SceneWithComments.UpdatedSinceLastRead` exists
- [ ] `MobileReadViewModel.UpdatedSinceLastRead` exists
- [ ] `ResolveSceneContentAndDiffAsync` returns `updatedSinceLastRead`
- [ ] `DesktopRead.cshtml` renders `.scene-updated-notice` when `UpdatedSinceLastRead` is true
- [ ] `MobileRead.cshtml` renders `.scene-updated-notice` when `UpdatedSinceLastRead` is true
- [ ] `.scene-updated-notice` CSS class added to stylesheet
- [ ] CSS version token bumped
- [ ] No inline styles introduced in any view
- [ ] Style leakage audit completed on both modified views
- [ ] TASKS.md Phase 2 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-3--phase-2-update-messaging`
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

- Dismiss action on the notice — no dismiss in this phase
- Update banner — Phase 3
- Banner dismissal tracking — Phase 3
- Change classification label — V-Sprint 4
- AI summary — V-Sprint 5
- Any author-facing changes
