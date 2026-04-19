---
mode: agent
description: V-Sprint 10 Phase 1 — Tree Service Extension
---

# V-Sprint 10 / Phase 1 — Tree Service Extension

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 3.5, 10 and V-Sprint 10
2. Read `REFACTORING.md` in full
3. Read `.github/copilot-instructions.md`
4. Read `DraftView.Domain/Interfaces/Services/ISectionTreeService.cs`
5. Read `DraftView.Application/Services/SectionTreeService.cs` — understand existing methods
6. Read `DraftView.Application.Tests/Services/SectionTreeServiceTests.cs` — understand existing coverage
7. Read `DraftView.Domain/Entities/Section.cs` — understand existing domain methods
8. Confirm the active branch is `vsprint-10--phase-1-tree-service-extension`
   — if not on this branch, stop and report
9. Run `git status` — confirm the working tree is clean with no uncommitted changes.
   If uncommitted changes exist that are not part of this phase, stop and report.
10. Run `.\test-summary.ps1` and record the baseline passing count before touching any code

---

## Goal

Extend `ISectionTreeService` with explicit section management operations:
`CreateSection`, `MoveSection`, and `DeleteSection`. These power the
drag-and-drop tree builder UI in Phase 2.

`GetOrCreateForUpload` already exists — do not duplicate it.
These new methods are the authoritative path for explicit author-driven tree management.

---

## Architecture Constraint

> `SectionTreeService.GetOrCreateForUpload` is the only path for manual section
> creation without a ScrivenerUuid.
>
> `CreateSection` is a new explicit creation path also without a ScrivenerUuid,
> intended for the tree builder UI only. Both paths are valid. `GetOrCreateForUpload`
> remains for import-driven creation; `CreateSection` is for UI-driven creation.

---

## TDD Sequence — Mandatory

Search existing `SectionTreeServiceTests.cs` before writing. Never duplicate a test.

1. Add stubs with `throw new NotImplementedException()`
2. Write all failing tests
3. Implement to make tests green
4. Run full test suite — zero regressions before committing

---

## Deliverable 1 — `ISectionTreeService` Extension

**File:** `DraftView.Domain/Interfaces/Services/ISectionTreeService.cs`

Add three new methods:

```csharp
/// <summary>
/// Creates a new Document or Folder section with an explicit title, parent, and sort order.
/// Called from the tree builder UI. Section will have ScrivenerUuid = null.
/// </summary>
Task<Section> CreateSectionAsync(
    Guid projectId,
    string title,
    NodeType nodeType,
    Guid? parentId,
    int? sortOrder,
    Guid authorId,
    CancellationToken ct = default);

/// <summary>
/// Moves a section to a new parent and/or sort order.
/// Validates that the move does not create a circular reference.
/// </summary>
Task MoveSectionAsync(
    Guid sectionId,
    Guid? newParentId,
    int newSortOrder,
    Guid authorId,
    CancellationToken ct = default);

/// <summary>
/// Soft-deletes a section and all its descendants.
/// Published sections are unpublished before soft-deletion.
/// </summary>
Task DeleteSectionAsync(
    Guid sectionId,
    Guid authorId,
    CancellationToken ct = default);
```

---

## Deliverable 2 — `SectionTreeService` Implementation

**File:** `DraftView.Application/Services/SectionTreeService.cs`

Read the existing implementation before modifying. Do not duplicate existing helpers.

### `CreateSectionAsync`

1. Validate title — throw `InvariantViolationException("I-TREE-TITLE", ...)` if null/empty
2. Validate `nodeType` is `Folder` or `Document`
3. If `parentId` is provided, verify the parent exists in the project
4. Resolve `sortOrder`: if null, use `MAX(SortOrder) + 1` among siblings
5. Create via `Section.CreateDocumentForUpload` for Document, or a new
   `Section.CreateFolder` for Folder — inspect existing factory methods before deciding
6. Save via unit of work
7. Return the created section

### `MoveSectionAsync`

1. Load the section — throw `EntityNotFoundException` if not found
2. Validate the move does not create a circular reference:
   - walk the ancestor chain of `newParentId` — if `sectionId` appears, throw
   - `InvariantViolationException("I-TREE-CIRCULAR", "Cannot move a section to one of its own descendants.")`
3. Update `section.ParentId` and `section.SortOrder`
4. Save via unit of work

### `DeleteSectionAsync`

1. Load the section — throw `EntityNotFoundException` if not found
2. Load all descendants recursively
3. For each section (including the root): unpublish if published, then soft-delete
4. Save via unit of work once (not per section)

---

## Deliverable 3 — Tests

**File:** `DraftView.Application.Tests/Services/SectionTreeServiceTests.cs`

Add to the existing test class. Check for duplicates first.

### `CreateSectionAsync` tests:
```
CreateSectionAsync_WithValidInput_CreatesSection
CreateSectionAsync_WithEmptyTitle_ThrowsInvariantViolation
CreateSectionAsync_DefaultsSortOrder_ToEndOfSiblingList
CreateSectionAsync_CreatedSection_HasNullScrivenerUuid
CreateSectionAsync_CanCreateFolder
CreateSectionAsync_CanCreateDocument
```

### `MoveSectionAsync` tests:
```
MoveSectionAsync_UpdatesParentAndSortOrder
MoveSectionAsync_WhenSectionNotFound_ThrowsEntityNotFoundException
MoveSectionAsync_WhenMovingToOwnDescendant_ThrowsInvariantViolation
MoveSectionAsync_CanMoveToRoot_WithNullParent
```

### `DeleteSectionAsync` tests:
```
DeleteSectionAsync_SoftDeletesSection
DeleteSectionAsync_SoftDeletesDescendants
DeleteSectionAsync_UnpublishesBeforeDeleting
DeleteSectionAsync_WhenSectionNotFound_ThrowsEntityNotFoundException
```

Run full test suite. Zero regressions.
Commit: `app: extend SectionTreeService with CreateSection, MoveSection, DeleteSection`

---

## Phase Gate — All Must Pass Before Marking Complete

Run `.\test-summary.ps1` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline
- [ ] Solution builds without errors
- [ ] `ISectionTreeService.CreateSectionAsync` exists and implemented
- [ ] `ISectionTreeService.MoveSectionAsync` exists and implemented
- [ ] `ISectionTreeService.DeleteSectionAsync` exists and implemented
- [ ] Circular reference guard in `MoveSectionAsync`
- [ ] Descendants soft-deleted in `DeleteSectionAsync`
- [ ] Published sections unpublished before soft-delete
- [ ] No EF migration required
- [ ] No controller changes
- [ ] No view changes
- [ ] TASKS.md Phase 1 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-10--phase-1-tree-service-extension`
- [ ] No warnings in test output linked to phase changes
- [ ] Refactor considered and applied where appropriate, tests green after refactor

---

## Do NOT implement in this phase

- Tree builder UI — Phase 2
- Sync project tree display — Phase 3
- Any view or controller changes
- Rename section — not in this sprint
