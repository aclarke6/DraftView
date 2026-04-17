---
mode: agent
description: V-Sprint 1 Phase 2 — Section Tree Service + Import Provider
---

# V-Sprint 1 / Phase 2 — Section Tree Service + Import Provider

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 3.4, 3.5, 10, and 11
2. Read `REFACTORING.md` sections 2, 5, 6, and 9
3. Read `.github/copilot-instructions.md`
4. Read `.github/instructions/versioning.instructions.md`
5. Confirm the active branch is `vsprint-1--phase-2-tree-service-import`
   - If not on this branch, stop and report — do not create the branch automatically
6. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Goal

Introduce the section tree management service and the manual file import pipeline.
No UI changes in this phase. All new services are wired in DI but no controller calls them yet.
All existing tests remain green. New services are fully tested.

---

## TDD Sequence — Mandatory for Every Deliverable

1. Create the stub with `throw new NotImplementedException()`
2. Write all failing tests for that stub
3. Confirm tests are red
4. Implement to make tests green
5. Run full test suite — zero regressions before proceeding to the next deliverable
6. Commit with an `app:` prefix before moving on

---

## Existing Patterns — Follow These Exactly

- All IDs are `Guid`, not `int`
- Application services live in `DraftView.Application/Services/`
- Application interfaces live in `DraftView.Application/Interfaces/` (for cross-cutting concerns)
  or `DraftView.Domain/Interfaces/Services/` (for domain service contracts)
- Test classes live in `DraftView.Application.Tests/Services/`
- Test method naming: `{Method}_{Condition}_{ExpectedOutcome}`
- All test classes and methods over 5 lines need XML summary comments (see `REFACTORING.md` section 9)
- Inject `IUnitOfWork` for persistence — never `DraftViewDbContext` directly in application services
- Inject `ISectionRepository` for section queries — already exists
- `IUnitOfWork` is in `DraftView.Domain/Interfaces/Repositories/IUnitOfWork.cs`
- Services registered in `DraftView.Web/Extensions/ServiceCollectionExtensions.cs`
  in the `AddApplicationServices` method
- `IRtfConverter` exists at `DraftView.Domain/Interfaces/Services/IRtfConverter.cs`
  — use it for RTF conversion, do not reimplement

---

## Deliverable 1 — `IImportProvider` Interface

**File:** `DraftView.Domain/Interfaces/Services/IImportProvider.cs`

```csharp
/// <summary>
/// Converts a file stream to HTML for ingestion into a Section's working state.
/// Import providers are conversion-only — they never write to the database.
/// The write to Section.HtmlContent is owned by ImportService.
/// </summary>
public interface IImportProvider
{
    /// <summary>File extension this provider handles, including the dot. E.g. ".rtf"</summary>
    string SupportedExtension { get; }

    /// <summary>Display name for this provider. E.g. "RTF"</summary>
    string ProviderName { get; }

    /// <summary>
    /// Converts the file stream to HTML. Returns the HTML string.
    /// Throws UnsupportedFileTypeException if the file cannot be parsed.
    /// </summary>
    Task<string> ConvertToHtmlAsync(
        Stream fileStream,
        CancellationToken cancellationToken = default);
}
```

No tests required for an interface definition. Proceed directly to Deliverable 2.

---

## Deliverable 2 — `UnsupportedFileTypeException`

**File:** `DraftView.Domain/Exceptions/UnsupportedFileTypeException.cs`

```csharp
/// <summary>
/// Thrown when an import provider cannot handle the supplied file extension.
/// </summary>
public class UnsupportedFileTypeException : Exception
{
    public string Extension { get; }

    public UnsupportedFileTypeException(string extension)
        : base($"No import provider is registered for file extension '{extension}'.")
    {
        Extension = extension;
    }
}
```

No tests required. Proceed to Deliverable 3.

---

## Deliverable 3 — `RtfImportProvider`

**File:** `DraftView.Application/Services/RtfImportProvider.cs`

Implements `IImportProvider`. Delegates conversion to the existing `IRtfConverter`.

```csharp
SupportedExtension → ".rtf"
ProviderName       → "RTF"
```

**`ConvertToHtmlAsync` implementation:**
- Write the stream to a temporary file
- Call `IRtfConverter.ConvertAsync` using a temp folder and temp UUID
- Delete the temp file after conversion
- If conversion returns null, throw `UnsupportedFileTypeException(".rtf")`
- Return the HTML string from the result

**Note on `IRtfConverter.ConvertAsync` signature:**
```csharp
Task<RtfConversionResult?> ConvertAsync(
    string scrivFolderPath,
    string uuid,
    CancellationToken ct = default)
```
It expects a folder path and a UUID. For manual uploads, create a temp folder, write the
RTF file as `{uuid}.rtf` inside it, call `ConvertAsync(tempFolder, uuid)`, then clean up.

### Tests — `DraftView.Application.Tests/Services/RtfImportProviderTests.cs`

Write all tests failing before implementing:

```
SupportedExtension_IsRtf
ProviderName_IsRtf
ConvertToHtmlAsync_WithValidRtfStream_ReturnsHtml
ConvertToHtmlAsync_WhenConverterReturnsNull_ThrowsUnsupportedFileTypeException
```

For `ConvertToHtmlAsync_WithValidRtfStream_ReturnsHtml` — mock `IRtfConverter` to return
a known `RtfConversionResult`. Verify the returned HTML matches.

Run full test suite. Zero regressions before proceeding.
Commit: `app: add RtfImportProvider implementing IImportProvider`

---

## Deliverable 4 — `SectionTreeNode` DTO

**File:** `DraftView.Application/Contracts/SectionTreeNode.cs`

```csharp
/// <summary>
/// Lightweight tree node used for rendering the section hierarchy
/// in upload parent dropdowns and the future tree builder UI.
/// </summary>
public sealed class SectionTreeNode
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid? ParentId { get; init; }
    public string Title { get; init; } = default!;
    public int SortOrder { get; init; }
    public NodeType NodeType { get; init; }
    public IReadOnlyList<SectionTreeNode> Children { get; init; }
        = Array.Empty<SectionTreeNode>();
}
```

No tests required for a DTO. Proceed to Deliverable 5.

---

## Deliverable 5 — `ISectionTreeService`

**File:** `DraftView.Domain/Interfaces/Services/ISectionTreeService.cs`

```csharp
/// <summary>
/// Manages the Section tree structure for author-managed projects.
/// GetOrCreateForUploadAsync is the only permitted creation path for
/// sections without a ScrivenerUuid.
/// </summary>
public interface ISectionTreeService
{
    /// <summary>
    /// Finds an existing Document section matching title + parentId within the project,
    /// or creates one. Created sections have ScrivenerUuid = null and NodeType = Document.
    /// SortOrder defaults to end of sibling list when not supplied.
    /// This is the ONLY place in the solution where a Section with ScrivenerUuid = null
    /// may be created.
    /// </summary>
    Task<Section> GetOrCreateForUploadAsync(
        Guid projectId,
        string title,
        Guid? parentId,
        int? sortOrder,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the full section hierarchy for a project as a tree of SectionTreeNodes.
    /// Soft-deleted sections are excluded. Ordered by SortOrder within each level.
    /// </summary>
    Task<IReadOnlyList<SectionTreeNode>> GetTreeAsync(
        Guid projectId,
        CancellationToken ct = default);
}
```

---

## Deliverable 6 — `SectionTreeService`

**File:** `DraftView.Application/Services/SectionTreeService.cs`

Implements `ISectionTreeService`.

**`GetOrCreateForUploadAsync` behaviour:**
1. Load all non-soft-deleted sections for the project via `ISectionRepository`
2. Find an existing section where `Title == title` (case-insensitive trim) AND
   `ParentId == parentId` AND `NodeType == Document` AND `IsSoftDeleted == false`
3. If found, return it
4. If not found, create a new `Section` via `Section.CreateDocument` (or equivalent
   factory — check `Section.cs` for the correct factory method name)
   - `ScrivenerUuid` must be null
   - `NodeType` must be `Document`
   - `SortOrder`: if supplied use it; otherwise use `(maxSiblingSort + 1)` or `1` if no siblings
4. Add via `ISectionRepository.AddAsync`
5. Save via `IUnitOfWork.SaveChangesAsync`
6. Return the section

**`GetTreeAsync` behaviour:**
1. Load all non-soft-deleted sections for the project
2. Build a recursive tree: root nodes (ParentId == null), then their children, ordered by SortOrder
3. Return as `IReadOnlyList<SectionTreeNode>`

### Tests — `DraftView.Application.Tests/Services/SectionTreeServiceTests.cs`

Write all tests failing before implementing:

```
GetOrCreateForUploadAsync_CreatesSection_WhenNoneExists
GetOrCreateForUploadAsync_ReturnsExisting_WhenTitleAndParentMatch
GetOrCreateForUploadAsync_IsCaseInsensitive_WhenMatchingTitle
GetOrCreateForUploadAsync_NeverCreatesDuplicate
GetOrCreateForUploadAsync_CreatedSection_HasNullScrivenerUuid
GetOrCreateForUploadAsync_CreatedSection_IsDocument
GetOrCreateForUploadAsync_DefaultsSortOrder_ToEndOfSiblingList
GetOrCreateForUploadAsync_UsesSortOrder_WhenSupplied
GetTreeAsync_ReturnsSortedHierarchy
GetTreeAsync_ExcludesSoftDeletedSections
GetTreeAsync_ReturnsEmptyList_WhenNoSections
```

Read `DraftView.Domain/Entities/Section.cs` before writing tests to confirm the correct
factory method signature for creating Document sections.

Run full test suite. Zero regressions before proceeding.
Commit: `app: add SectionTreeService with GetOrCreateForUploadAsync and GetTreeAsync`

---

## Deliverable 7 — `IImportService`

**File:** `DraftView.Domain/Interfaces/Services/IImportService.cs`

```csharp
/// <summary>
/// Orchestrates the manual file import flow.
/// Resolves the correct IImportProvider by file extension,
/// converts the file to HTML, and writes the result to Section.HtmlContent.
/// Import never creates SectionVersion records.
/// </summary>
public interface IImportService
{
    /// <summary>
    /// Converts the file stream to HTML via the appropriate IImportProvider
    /// and writes the result to Section.HtmlContent.
    /// Updates ContentHash. Sets ContentChangedSincePublish if a SectionVersion exists.
    /// Throws UnsupportedFileTypeException if no provider handles the file extension.
    /// </summary>
    Task ImportAsync(
        Guid projectId,
        Guid sectionId,
        Stream fileStream,
        string fileName,
        Guid authorId,
        CancellationToken cancellationToken = default);
}
```

---

## Deliverable 8 — `ImportService`

**File:** `DraftView.Application/Services/ImportService.cs`

Implements `IImportService`. Takes `IEnumerable<IImportProvider>` in the constructor
so all registered providers are available for extension-based resolution.

**`ImportAsync` sequence:**
1. Resolve provider: find `IImportProvider` where `SupportedExtension` matches the file
   extension of `fileName` (case-insensitive, including the dot)
2. If no provider found, throw `UnsupportedFileTypeException(extension)`
3. Call `provider.ConvertToHtmlAsync(fileStream, cancellationToken)`
4. Load the section via `ISectionRepository.GetByIdAsync(sectionId)`
5. If section not found, throw `EntityNotFoundException`
6. Call `section.UpdateHtmlContent(html, hash)` or equivalent domain method
   — check `Section.cs` for the correct method name
7. Compute `ContentHash` from the HTML — use `Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(html)))`
8. Check whether a `SectionVersion` exists via `ISectionVersionRepository.GetLatestAsync(sectionId)`
   — if one exists, ensure `ContentChangedSincePublish` is set (check Section domain method)
9. Save via `IUnitOfWork.SaveChangesAsync`

**Important:** `ImportService` never creates `SectionVersion` records. It writes to
`Section.HtmlContent` only.

### Tests — `DraftView.Application.Tests/Services/ImportServiceTests.cs`

Write all tests failing before implementing:

```
ImportAsync_WritesHtmlToSection
ImportAsync_UpdatesContentHash
ImportAsync_SetsDirtyFlag_WhenVersionExists
ImportAsync_DoesNotSetDirtyFlag_WhenNoVersionExists
ImportAsync_Throws_ForUnsupportedExtension
ImportAsync_Throws_WhenSectionNotFound
ImportAsync_NeverCreatesVersion
```

For `ImportAsync_NeverCreatesVersion` — verify that `ISectionVersionRepository.AddAsync`
is never called regardless of input.

Run full test suite. Zero regressions before proceeding.
Commit: `app: add ImportService orchestrating file import via IImportProvider`

---

## Deliverable 9 — DI Registration

**File:** `DraftView.Web/Extensions/ServiceCollectionExtensions.cs`

In `AddApplicationServices`:

```csharp
services.AddScoped<ISectionTreeService, SectionTreeService>();
services.AddScoped<IImportService, ImportService>();
services.AddScoped<IImportProvider, RtfImportProvider>();
```

`IImportProvider` is registered with `AddScoped` — `ImportService` receives
`IEnumerable<IImportProvider>` which EF DI resolves automatically from all registered
implementations.

Run `dotnet build --nologo` to confirm the solution compiles.
Run `dotnet test --nologo` — full suite green.
Commit: `app: register SectionTreeService, ImportService, RtfImportProvider in DI`

---

## Section.cs — Read Before Implementing

Before implementing `SectionTreeService` and `ImportService`, read
`DraftView.Domain/Entities/Section.cs` to confirm:
- The correct factory method for creating Document sections without a ScrivenerUuid
- The correct domain method for updating HtmlContent and ContentHash
- The correct domain method or property for ContentChangedSincePublish

Do not guess these method names. Read the file.

---

## Phase Gate — All Must Pass Before Marking Complete

Run `dotnet test --nologo` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline recorded at session start
- [ ] Solution builds without errors (`dotnet build --nologo`)
- [ ] `IImportProvider` exists in `DraftView.Domain/Interfaces/Services`
- [ ] `UnsupportedFileTypeException` exists in `DraftView.Domain/Exceptions`
- [ ] `RtfImportProvider` exists in `DraftView.Application/Services`
- [ ] `SectionTreeNode` exists in `DraftView.Application/Contracts`
- [ ] `ISectionTreeService` exists in `DraftView.Domain/Interfaces/Services`
- [ ] `SectionTreeService` exists in `DraftView.Application/Services`
- [ ] `IImportService` exists in `DraftView.Domain/Interfaces/Services`
- [ ] `ImportService` exists in `DraftView.Application/Services`
- [ ] All three services registered in `ServiceCollectionExtensions.cs`
- [ ] No controller changes — Phase 5 only
- [ ] No view changes — Phase 5 only
- [ ] No `SectionVersion` records created anywhere in this phase
- [ ] No inline styles introduced in any view
- [ ] TASKS.md Phase 2 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-1--phase-2-tree-service-import`

## Do NOT implement in this phase

- `IVersioningService` or `VersioningService` — Phase 3
- Any controller changes — Phase 5
- Any Razor view changes — Phase 5
- Republish button or upload UI — Phase 5
- `.docx` import support — deferred post V-Sprint 1