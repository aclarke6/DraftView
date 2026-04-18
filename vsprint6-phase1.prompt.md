---
mode: agent
description: V-Sprint 6 Phase 1 — Per-Document Publishing Application Layer
---

# V-Sprint 6 / Phase 1 — Per-Document Publishing Application Layer

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 7, 8, 11.3 and V-Sprint 6
2. Read `REFACTORING.md` in full
3. Read `.github/copilot-instructions.md`
4. Read `DraftView.Domain/Interfaces/Services/IVersioningService.cs`
5. Read `DraftView.Application/Services/VersioningService.cs` — understand `RepublishChapterAsync`
6. Read `DraftView.Application.Tests/Services/VersioningServiceTests.cs` — understand existing coverage
7. Confirm the active branch is `vsprint-6--phase-1-per-document-application`
   — if not on this branch, stop and report
8. Run `git status` — confirm the working tree is clean with no uncommitted changes.
   If uncommitted changes exist that are not part of this phase, stop and report.
9. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Goal

Add `RepublishSectionAsync` to `IVersioningService` and implement it in `VersioningService`.
This enables per-document publishing — a single `Document` section can be versioned
independently of its parent chapter.

Also add `RevokeLatestVersionAsync` to enable the Revoke action on the Publishing Page
(Phase 2). Both methods are application-layer only — no UI in this phase.

Source-agnostic: both methods work identically for sync-sourced and import-sourced Documents.

---

## TDD Sequence — Mandatory

Search existing `VersioningServiceTests.cs` before writing any new tests.
Never write a duplicate test.

1. Add method stubs with `throw new NotImplementedException()`
2. Write all failing tests
3. Confirm tests are red
4. Implement to make tests green
5. Run full test suite — zero regressions before committing

---

## Existing Patterns — Follow These Exactly

- `RepublishChapterAsync` is the existing pattern — read it before implementing
- `SectionVersion.Create(section, authorId, nextVersionNumber)` is the only creation path
- `ISectionVersionRepository.GetMaxVersionNumberAsync` gives the current max version number
- Classification and AI summary are wired in `RepublishChapterAsync` — apply the same pattern
  in `RepublishSectionAsync` so per-document versions also get classification and summary
- `IHtmlDiffService`, `IChangeClassificationService`, `IAiSummaryService` are already injected
  into `VersioningService` — do not add duplicates

---

## Deliverable 1 — `IVersioningService` Extension

**File:** `DraftView.Domain/Interfaces/Services/IVersioningService.cs`

Add two new methods:

```csharp
/// <summary>
/// Creates a SectionVersion for a single Document section.
/// Sets Section.IsPublished = true.
/// Throws if the section does not exist, is not a Document, is soft-deleted,
/// or has no HtmlContent.
/// </summary>
Task RepublishSectionAsync(
    Guid sectionId,
    Guid authorId,
    CancellationToken ct = default);

/// <summary>
/// Revokes the latest SectionVersion for a single Document section.
/// Rolls back to the previous version; that version becomes reader-visible.
/// If no previous version exists, sets Section.IsPublished = false.
/// Throws if the section does not exist, is not a Document, or has no versions.
/// Revoke is not permitted when only one version exists and it is the
/// current published version — use Unpublish instead.
/// </summary>
Task RevokeLatestVersionAsync(
    Guid sectionId,
    Guid authorId,
    CancellationToken ct = default);
```

---

## Deliverable 2 — `RepublishSectionAsync` Implementation

**File:** `DraftView.Application/Services/VersioningService.cs`

Implement `RepublishSectionAsync`:

1. Load the section by `sectionId` — throw `EntityNotFoundException` if not found
2. Validate: `NodeType == Document`, not soft-deleted, `HtmlContent` not null/empty
   — throw `InvariantViolationException` for each violation with a clear invariant code
3. Get max version number via `ISectionVersionRepository.GetMaxVersionNumberAsync`
4. Create new version: `SectionVersion.Create(section, authorId, maxVersion + 1)`
5. Apply classification (same pattern as `RepublishChapterAsync`):
   - Load all previous versions
   - Find the previous version
   - If previous exists, compute diff, classify, call `SetChangeClassification`
   - Wrap in try/catch — classification failure must not block publish
6. Generate AI summary (same pattern as `RepublishChapterAsync`):
   - Call `aiSummaryService.GenerateSummaryAsync(previousHtml, section.HtmlContent, ct)`
   - If non-null, call `SetAiSummary`
7. Add version via `sectionVersionRepository.AddAsync`
8. Call `section.PublishAsPartOfChapter(section.ContentHash ?? string.Empty)` to mark published
9. Save via `unitOfWork.SaveChangesAsync(ct)`

---

## Deliverable 3 — `RevokeLatestVersionAsync` Implementation

**File:** `DraftView.Application/Services/VersioningService.cs`

Implement `RevokeLatestVersionAsync`:

1. Load the section by `sectionId` — throw `EntityNotFoundException` if not found
2. Validate: `NodeType == Document`, not soft-deleted
3. Load all versions via `GetAllBySectionIdAsync`
4. If no versions exist — throw `InvariantViolationException("I-VER-REVOKE-NONE", "No versions exist to revoke.")`
5. Find the latest version (highest `VersionNumber`)
6. If only one version exists — throw `InvariantViolationException("I-VER-REVOKE-LAST", "Cannot revoke the only version. Use Unpublish instead.")`
7. Remove the latest version: call `sectionVersionRepository.DeleteAsync(latestVersion.Id, ct)`
   — check if `DeleteAsync` exists on the repository; if not, add it (see Deliverable 4)
8. Find the new latest version (second highest `VersionNumber`)
9. Update `Section.IsPublished = true`, `Section.PublishedAt` to remain set
   — the previous version is now the visible one; no domain method change needed
10. Save via `unitOfWork.SaveChangesAsync(ct)`

---

## Deliverable 4 — `ISectionVersionRepository.DeleteAsync`

**File:** `DraftView.Domain/Interfaces/Repositories/ISectionVersionRepository.cs`

Check if `DeleteAsync` exists. If not, add:

```csharp
/// <summary>
/// Permanently deletes a SectionVersion record.
/// This is the only case of physical deletion in the versioning system.
/// Used exclusively by RevokeLatestVersionAsync.
/// </summary>
Task DeleteAsync(Guid versionId, CancellationToken ct = default);
```

**File:** `DraftView.Infrastructure/Persistence/Repositories/SectionVersionRepository.cs`

Implement:

```csharp
public async Task DeleteAsync(Guid versionId, CancellationToken ct = default)
{
    var version = await _context.SectionVersions
        .FirstOrDefaultAsync(v => v.Id == versionId, ct);
    if (version is not null)
        _context.SectionVersions.Remove(version);
}
```

---

## Deliverable 5 — Tests

**File:** `DraftView.Application.Tests/Services/VersioningServiceTests.cs`

Add to the existing test class. Check for duplicates first.

### `RepublishSectionAsync` tests:

```
RepublishSectionAsync_WithValidDocument_CreatesVersion
RepublishSectionAsync_WithFolderSection_ThrowsInvariantViolation
RepublishSectionAsync_WithSoftDeletedSection_ThrowsInvariantViolation
RepublishSectionAsync_WithNullHtmlContent_ThrowsInvariantViolation
RepublishSectionAsync_IncrementsVersionNumber
RepublishSectionAsync_SetsChangeClassification_WhenPreviousVersionExists
RepublishSectionAsync_SetsAiSummary_WhenServiceReturnsSummary
RepublishSectionAsync_StillPublishes_WhenClassificationFails
```

### `RevokeLatestVersionAsync` tests:

```
RevokeLatestVersionAsync_WithMultipleVersions_DeletesLatest
RevokeLatestVersionAsync_WithNoVersions_ThrowsInvariantViolation
RevokeLatestVersionAsync_WithSingleVersion_ThrowsInvariantViolation
RevokeLatestVersionAsync_WithFolderSection_ThrowsInvariantViolation
```

Run full test suite. Zero regressions.
Commit: `app: add RepublishSectionAsync and RevokeLatestVersionAsync to VersioningService`

---

## Phase Gate — All Must Pass Before Marking Complete

Run `dotnet test --nologo` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline
- [ ] Solution builds without errors
- [ ] `IVersioningService.RepublishSectionAsync` exists
- [ ] `IVersioningService.RevokeLatestVersionAsync` exists
- [ ] `VersioningService.RepublishSectionAsync` implemented
- [ ] `VersioningService.RevokeLatestVersionAsync` implemented
- [ ] `ISectionVersionRepository.DeleteAsync` exists and implemented
- [ ] Classification applied in `RepublishSectionAsync` — same pattern as chapter republish
- [ ] AI summary applied in `RepublishSectionAsync` — same pattern as chapter republish
- [ ] No controller changes
- [ ] No view changes
- [ ] No EF migration required
- [ ] TASKS.md Phase 1 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-6--phase-1-per-document-application`
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
quality, as per the refactoring guidelines. In particular:

- `RepublishSectionAsync` and `RepublishChapterAsync` share significant logic
  (version creation, classification, AI summary). If the duplication is significant,
  extract a private `CreateVersionForDocument` helper that both methods call.
- Run full test suite after any extraction to confirm green.

---

## Do NOT implement in this phase

- Publishing Page UI — Phase 2
- Any controller changes
- Any view changes
- Chapter-level Revoke — out of scope for this sprint
