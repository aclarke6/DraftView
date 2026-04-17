---
mode: agent
description: V-Sprint 1 Phase 3 — Versioning Application Layer
---

# V-Sprint 1 / Phase 3 — Versioning Application Layer

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 7, 8, and V-Sprint 1 Phase 3
2. Read `REFACTORING.md` sections 2, 5, 6, and 9
3. Read `.github/copilot-instructions.md`
4. Read `.github/instructions/versioning.instructions.md`
5. Read `DraftView.Application/Services/PublicationService.cs` — understand the existing
   publication pattern before implementing `VersioningService`
6. Read `DraftView.Domain/Interfaces/Repositories/ISectionRepository.cs` — confirm
   available repository methods
7. Confirm the active branch is `vsprint-1--phase-3-versioning-service`
   — if not on this branch, stop and report
8. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Goal

Introduce `IVersioningService` and `VersioningService`. The service creates `SectionVersion`
snapshots when the author republishes a chapter. It is registered in DI but no controller
calls it yet — that is Phase 5.

No UI changes in this phase. No controller changes. No view changes.

---

## TDD Sequence — Mandatory

1. Create the stub with `throw new NotImplementedException()`
2. Write all failing tests
3. Confirm tests are red
4. Implement to make tests green
5. Run full test suite — zero regressions before proceeding
6. Commit with `app:` prefix

---

## Existing Patterns — Follow These Exactly

- All IDs are `Guid`
- Application service interfaces in `DraftView.Domain/Interfaces/Services/`
- Application service implementations in `DraftView.Application/Services/`
- Test classes in `DraftView.Application.Tests/Services/`
- Test method naming: `{Method}_{Condition}_{ExpectedOutcome}`
- XML summary on every class and every method over 5 lines (`REFACTORING.md` section 9)
- Test class XML summary states what is covered and what is excluded
- `IUnitOfWork` for persistence — never `DraftViewDbContext` directly
- `ISectionRepository.GetAllDescendantsAsync` retrieves all descendants of a folder
- `ISectionVersionRepository.GetMaxVersionNumberAsync` returns 0 when no versions exist
- `SectionVersion.Create(section, authorId, nextVersionNumber)` is the only creation path
- Study `PublicationService.cs` before writing — `VersioningService` follows the same shape

---

## Key Architectural Rule

> Sync never creates versions. Import never creates versions.
> Only `VersioningService.RepublishChapterAsync` creates `SectionVersion` records.
> This rule is absolute. No other class may call `SectionVersion.Create`.

---

## Deliverable 1 — `IVersioningService`

**File:** `DraftView.Domain/Interfaces/Services/IVersioningService.cs`

```csharp
/// <summary>
/// Creates SectionVersion snapshots when the author publishes a chapter.
/// This is the only permitted path for SectionVersion creation.
/// Sync and import never call this service.
/// </summary>
public interface IVersioningService
{
    /// <summary>
    /// Creates a SectionVersion for each non-soft-deleted Document descendant
    /// of the given chapter. Sets Section.IsPublished = true on each versioned section.
    /// Throws if the chapter does not exist, is not a Folder, or has no publishable
    /// Document descendants.
    /// </summary>
    Task RepublishChapterAsync(
        Guid chapterId,
        Guid authorId,
        CancellationToken ct = default);
}
```

No tests required for an interface definition. Proceed to Deliverable 2.

---

## Deliverable 2 — `VersioningService` stub + tests + implementation

**File:** `DraftView.Application/Services/VersioningService.cs`

### Step 1 — Create the stub

```csharp
public class VersioningService(
    ISectionRepository sectionRepository,
    ISectionVersionRepository sectionVersionRepository,
    IUnitOfWork unitOfWork) : IVersioningService
{
    public Task RepublishChapterAsync(Guid chapterId, Guid authorId, CancellationToken ct = default)
        => throw new NotImplementedException();
}
```

### Step 2 — Write failing tests first

**File:** `DraftView.Application.Tests/Services/VersioningServiceTests.cs`

Test class XML summary required — state what is covered and what is excluded.

Write all tests failing before implementing `RepublishChapterAsync`:

```
RepublishChapterAsync_WithValidChapter_CreatesVersionPerDocument
RepublishChapterAsync_WithValidChapter_SetsIsPublishedOnEachDocument
RepublishChapterAsync_WithValidChapter_VersionNumberStartsAtOne
RepublishChapterAsync_VersionNumberIncrements_WhenVersionsAlreadyExist
RepublishChapterAsync_WithNoDocuments_ThrowsInvariantViolation
RepublishChapterAsync_WithFolderSection_ThrowsInvariantViolation
RepublishChapterAsync_IgnoresSoftDeletedDocuments
RepublishChapterAsync_WorksForManualProject
RepublishChapterAsync_SavesOnce
```

**Test helpers to use:**

```csharp
private static Section MakeChapter(Guid projectId) =>
    Section.CreateFolder(projectId, Guid.NewGuid().ToString(), "Chapter 1", null, 0);

private static Section MakeDocument(Guid projectId, Guid chapterId) =>
    Section.CreateDocument(projectId, Guid.NewGuid().ToString(),
        "Scene 1", chapterId, 0, "<p>content</p>", "hash", null);

private static Section MakeManualDocument(Guid projectId, Guid chapterId)
{
    var section = Section.CreateDocumentForUpload(projectId, "Scene 1", chapterId, 0);
    section.UpdateContent("<p>content</p>", "hash");
    return section;
}
```

**Key test setup notes:**

- `RepublishChapterAsync_WithValidChapter_CreatesVersionPerDocument`: mock
  `sectionRepository.GetByIdAsync` to return the chapter; mock
  `sectionRepository.GetAllDescendantsAsync` to return two documents; mock
  `sectionVersionRepository.GetMaxVersionNumberAsync` to return 0 for both; verify
  `sectionVersionRepository.AddAsync` called twice.

- `RepublishChapterAsync_VersionNumberIncrements_WhenVersionsAlreadyExist`: mock
  `GetMaxVersionNumberAsync` to return 2 for one section; verify `AddAsync` called with
  a version where `VersionNumber == 3`.

- `RepublishChapterAsync_WithFolderSection_ThrowsInvariantViolation`: pass a Folder
  section as `chapterId` — the chapter itself must be a Folder. The test verifies passing
  a Document ID throws `InvariantViolationException`.

- `RepublishChapterAsync_WorksForManualProject`: use `Section.CreateDocumentForUpload`
  (via `MakeManualDocument`) to confirm versioning works for sections without ScrivenerUuid.

- `RepublishChapterAsync_SavesOnce`: verify `unitOfWork.SaveChangesAsync` called exactly once
  regardless of how many documents are versioned.

Confirm tests are red before proceeding.

### Step 3 — Implement `RepublishChapterAsync`

**Sequence:**

1. Load chapter via `sectionRepository.GetByIdAsync(chapterId, ct)`
   — throw `EntityNotFoundException` if null
2. Validate `chapter.NodeType == NodeType.Folder`
   — throw `InvariantViolationException("I-VER-CHAPTER", ...)` if not
3. Load all descendants via `sectionRepository.GetAllDescendantsAsync(chapterId, ct)`
4. Filter to non-soft-deleted Documents with non-null, non-empty HtmlContent
5. If none, throw `InvariantViolationException("I-VER-NO-DOCS", "Chapter has no publishable Document sections.")`
6. For each publishable document:
   a. Call `sectionVersionRepository.GetMaxVersionNumberAsync(document.Id, ct)` to get current max
   b. Call `SectionVersion.Create(document, authorId, maxVersion + 1)`
   c. Call `sectionVersionRepository.AddAsync(version, ct)`
   d. Set `document.IsPublished = true` via `document.PublishAsPartOfChapter(document.ContentHash ?? string.Empty)`
7. Call `unitOfWork.SaveChangesAsync(ct)` exactly once after all versions created

**Important:** Do NOT call `chapter.MarkAsPublishedContainer()` in `VersioningService`.
The `PublicationService` handles folder publication state. `VersioningService` only creates
`SectionVersion` records for Document sections and sets their `IsPublished` state.

Run failing tests after stub, implement to green, run full suite.

Commit: `app: add VersioningService.RepublishChapterAsync with full TDD coverage`

---

## Deliverable 3 — DI Registration

**File:** `DraftView.Web/Extensions/ServiceCollectionExtensions.cs`

In `AddApplicationServices`, add:

```csharp
services.AddScoped<IVersioningService, VersioningService>();
```

Run `dotnet build --nologo` to confirm the solution compiles.
Run `dotnet test --nologo` — full suite green.

Commit: `app: register VersioningService in DI`

---

## Phase Gate — All Must Pass Before Marking Complete

Run `dotnet test --nologo` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline recorded at session start
- [ ] Solution builds without errors
- [ ] `IVersioningService` exists in `DraftView.Domain/Interfaces/Services`
- [ ] `VersioningService` exists in `DraftView.Application/Services`
- [ ] `IVersioningService` registered in `ServiceCollectionExtensions.cs`
- [ ] `VersioningService` never calls `chapter.MarkAsPublishedContainer()`
- [ ] `SectionVersion.Create` called only inside `VersioningService`
- [ ] `unitOfWork.SaveChangesAsync` called exactly once per `RepublishChapterAsync` call
- [ ] No controller changes
- [ ] No view changes
- [ ] No migration required — no schema change in this phase
- [ ] TASKS.md Phase 3 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-1--phase-3-versioning-service`

## Do NOT implement in this phase

- `RevokeLatestVersionAsync` — V-Sprint 6
- `RepublishSectionAsync` (per-document publishing) — V-Sprint 6
- Reader view changes — Phase 4
- Republish button or any UI — Phase 5
- Notifications on republish — deferred