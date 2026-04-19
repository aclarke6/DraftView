---
applyTo: "**/Domain/**,**/Application/**,**/Infrastructure/**"
---

# DraftView — Versioning and Ingestion Instructions

Full architecture in `Publishing And Versioning Architecture.md`.
Full sprint plan and task list in `TASKS.md`.

---

## Two-Layer Model — Absolute Rules

The system is divided into two layers. These rules are non-negotiable.

**Ingestion layer** — writes working state only.
**Platform layer** — owns versioning, publishing, reader access, notifications.

| Rule | No exceptions |
|------|---------------|
| Sync (`ISyncProvider`) writes to `Section.HtmlContent` only | Never touches `SectionVersion` |
| Import (`IImportProvider`) writes to `Section.HtmlContent` only | Never touches `SectionVersion` |
| `SectionVersion` records are created only by `VersioningService` | No other class may call `SectionVersion.Create()` |
| `SectionVersion.HtmlContent` is immutable after creation | No setter, no update path, no migration to change it |
| `Section.HtmlContent` is working state — not reader-facing | Readers always read from `SectionVersion` |

---

## SectionVersion

### Entity invariants (enforced in factory method — not by caller)

- `NodeType` must be `Document` — Folder nodes cannot be versioned
- Section must not be soft-deleted
- `HtmlContent` must not be null or empty
- `VersionNumber` is 1-based, scoped per `SectionId`, assigned as `MAX(VersionNumber) + 1`
- `VersionNumber` is never reused

### Factory method signature

```csharp
// Only valid creation path
public static SectionVersion Create(Section section, int authorId)
```

### Properties

| Property | Rule |
|----------|------|
| `HtmlContent` | Snapshot of `Section.HtmlContent` at creation time. Immutable |
| `ContentHash` | Hash of `HtmlContent` at creation time. Immutable |
| `ChangeClassification` | Nullable. Populated by `IChangeClassificationService` (V-Sprint 4) |
| `AiSummary` | Nullable. Populated by `IAiSummaryService` (V-Sprint 5) |
| `CreatedAt` | Set at creation. Immutable |

---

## VersioningService

### What it does

- `RepublishChapterAsync(int chapterId, int authorId, CancellationToken ct)`
  Loads the chapter (Folder). Gets all non-soft-deleted Document descendants.
  Calls `SectionVersion.Create()` for each. Persists. Sets `Section.IsPublished = true`.

- `RepublishSectionAsync(int sectionId, int authorId, CancellationToken ct)` — V-Sprint 6
  Single Document republish.

### What it must never do

- Query `Section.HtmlContent` for any purpose other than snapshotting into a version
- Modify `Section.HtmlContent`
- Be called from within `ISyncProvider` or `IImportProvider` implementations
- Create a version when `HtmlContent` has not changed since the last version (advisory — author may still force republish)

---

## IImportProvider

```csharp
public interface IImportProvider
{
    string SupportedExtension { get; }  // e.g. ".rtf"
    string ProviderName { get; }        // e.g. "RTF"

    Task<string> ConvertToHtmlAsync(
        Stream fileStream,
        CancellationToken cancellationToken = default);
}
```

- Converts a file stream to HTML only
- Knows nothing about sections, projects, authors, or versions
- Never writes to the database
- `ImportService` owns the write to `Section.HtmlContent`

### Current implementations

| Class | Extension |
|-------|-----------|
| `RtfImportProvider` | `.rtf` — delegates to existing `RtfConverter` |
| `WordImportProvider` | `.docx` — deferred |

---

## ISyncProvider

```csharp
public interface ISyncProvider
{
    Task SyncProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<bool> IsAvailableAsync(int projectId, CancellationToken cancellationToken = default);
}
```

- `ScrivenerSyncService` is the current implementation
- Writes to `Section.HtmlContent` and `Section.ContentHash` only
- Updates `Project` sync status fields only
- Never calls `VersioningService`
- Never creates or modifies `SectionVersion` records

---

## ImportService

Orchestrates manual upload. The only class that calls `IImportProvider` and writes the result to a `Section`.

```csharp
Task ImportAsync(
    int projectId,
    int sectionId,
    Stream fileStream,
    string fileName,
    int authorId,
    CancellationToken cancellationToken = default);
```

Sequence:
1. Resolve provider by file extension — throw `UnsupportedFileTypeException` if none found
2. Call `provider.ConvertToHtmlAsync(fileStream)`
3. Write HTML to `Section.HtmlContent`
4. Recompute `Section.ContentHash`
5. Set `Section.ContentChangedSincePublish = true` if a `SectionVersion` exists for this section
6. Save via unit of work

---

## SectionTreeService

The single creation point for manually-managed sections.

```csharp
// Only method that creates a Section with ScrivenerUuid = null
Task<Section> GetOrCreateForUploadAsync(
    int projectId,
    string title,
    int? parentId,
    int? sortOrder,
    CancellationToken ct = default);

// Powers upload parent dropdown and future tree builder
Task<IReadOnlyList<SectionTreeNode>> GetTreeAsync(
    int projectId,
    CancellationToken ct = default);
```

- `GetOrCreateForUploadAsync` is the **only** place in the solution where a `Section` with `ScrivenerUuid = null` is ever instantiated
- Created sections always have `NodeType = Document`
- `SortOrder` defaults to end of sibling list when not supplied
- Never creates a duplicate — finds existing section by title + parent before creating

---

## Project.ProjectType

```csharp
public enum ProjectType
{
    ScrivenerDropbox = 0,  // Synced via Dropbox / Scrivener pipeline
    Manual = 1             // Author-managed, no sync source
}
```

- `ScrivenerDropbox` projects: `DropboxPath`, sync status fields are required
- `Manual` projects: `DropboxPath` and all sync fields are null — this is valid, not an error
- Sync is never triggered for `Manual` projects
- Import is available for both project types

---

## Comment.SectionVersionId

- Nullable FK to `SectionVersion`
- Set at comment creation time to the current latest `SectionVersion.Id` for the section
- Null when no version exists yet (pre-versioning section)
- Never updated after creation
- Comments are never hidden when versions change — only labelled "made on version N"

---

## ReadEvent.LastReadVersionNumber

- Nullable int
- Set to `SectionVersion.VersionNumber` when a reader opens a section that has a current version
- Null when reader opened before any version existed
- Drives update messaging in V-Sprint 3
- Updated via `ReadEvent.UpdateLastReadVersion(int versionNumber)` domain method only

---

## Reader Content Resolution

Resolve reader-facing content in this order:

1. Load latest `SectionVersion` via `ISectionVersionRepository.GetLatestAsync(sectionId)`
2. If a version exists: serve `sectionVersion.HtmlContent`
3. If no version exists (pre-versioning published section): fall back to `Section.HtmlContent`

The fallback is temporary. It is removed once all sections have been republished at least once.
Never serve `Section.HtmlContent` to a reader when a `SectionVersion` exists.

---

## Revoke

- Author may revoke the latest version only
- Previous version becomes reader-visible
- If the only version is revoked: `Section.IsPublished = false`
- Revoke is not available when one version exists and the author wants to hide it — use Unpublish instead
- Revoke never physically deletes a `SectionVersion` (physical deletion is version retention only — V-Sprint 9)

---

## TDD Requirements for This Layer

All Domain, Application, and Infrastructure changes require TDD. See `REFACTORING.md` for
the full commenting standard. Key tests required for every new versioning artefact:

**`SectionVersion` domain tests (write failing first):**
- `Create_WithDocumentSection_CreatesVersionWithSnapshot`
- `Create_WithFolderSection_ThrowsInvariantViolation`
- `Create_WithSoftDeletedSection_ThrowsInvariantViolation`
- `Create_WithNullHtmlContent_ThrowsInvariantViolation`
- `Create_AssignsCorrectVersionNumber`
- `Create_FirstVersion_AssignsVersionNumberOne`

**`VersioningService` application tests (write failing first):**
- `RepublishChapterAsync_WithValidChapter_CreatesVersionPerDocument`
- `RepublishChapterAsync_WithNoDocuments_ThrowsInvariantViolation`
- `RepublishChapterAsync_WithFolderSection_ThrowsInvariantViolation`
- `RepublishChapterAsync_IgnoresSoftDeletedDocuments`
- `RepublishChapterAsync_VersionNumberIncrements`
- `RepublishChapterAsync_WorksForManualProject`

**`SectionTreeService` application tests (write failing first):**
- `GetOrCreateForUploadAsync_CreatesSection_WhenNoneExists`
- `GetOrCreateForUploadAsync_ReturnsExisting_WhenTitleAndParentMatch`
- `GetOrCreateForUploadAsync_NeverCreatesDuplicate`
- `GetOrCreateForUploadAsync_CreatedSection_HasNullScrivenerUuid`
- `GetOrCreateForUploadAsync_CreatedSection_IsDocument`

**`ImportService` application tests (write failing first):**
- `ImportAsync_WritesHtmlToSection`
- `ImportAsync_UpdatesContentHash`
- `ImportAsync_SetsDirtyFlag_WhenVersionExists`
- `ImportAsync_DoesNotSetDirtyFlag_WhenNoVersionExists`
- `ImportAsync_Throws_ForUnsupportedExtension`