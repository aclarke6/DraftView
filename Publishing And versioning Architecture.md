# DraftView Platform Architecture — Publishing and Versioning (v4.3)

---

See DraftView Git Rules.md for versioning and branching strategy.

## Revision History

| Version | Change |
|---------|--------|
| v4.0 | Core versioning backbone, sync provider abstraction, phased delivery model |
| v4.1 | Manual upload as first-class ingestion channel. `ProjectType` enum. `IImportProvider` interface. `SectionTreeService` creation gate. `Comment.SectionVersionId` anchoring. |
| v4.2 | V-Sprint 1 Phase 1 complete. `SectionVersion`, `ChangeClassification`, `ProjectType` in Domain. `ISectionVersionRepository` + EF implementation. `ReadEvent.LastReadVersionNumber`. `Comment.SectionVersionId`. Migration `AddVersioningAndManualUpload` applied to production 2026-04-17. 529 tests. |
| v4.3 | V-Sprint 1 Phase 2 complete. `IImportProvider`, `IImportService`, `ISectionTreeService`, `UnsupportedFileTypeException`, `SectionTreeNode` in Domain. `RtfImportProvider`, `ImportService`, `SectionTreeService` in Application. `Section.CreateDocumentForUpload` factory + domain tests. All services registered in DI. Forensic review passed. 558 tests. |

---

## 1. Purpose

Define how DraftView:

- Ingests content from multiple sources
- Manages content as a platform-owned private store
- Tracks changes to prose
- Converts working state into published versions
- Controls when versions are published
- Communicates updates to readers
- Maintains a stable and predictable reading experience

This document defines both **platform architecture** and **publishing and versioning behaviour**.

---

## 2. Core Principles

> DraftView owns the content store.
> Ingestion channels are plugins.
> Authors publish deliberately.
> Readers see only stable versions.
> DraftView works without Scrivener.

---

## 3. Two-Layer Architecture

DraftView is structured as two distinct layers. Nothing from the ingestion layer leaks into the platform layer.

```
INGESTION LAYER
┌─────────────────────────────────────────────────┐
│  ISyncProvider implementations                  │
│  ├── ScrivenerSyncService (current)             │
│  └── [any future sync provider]                 │
│                                                 │
│  IImportProvider implementations                │
│  ├── RtfImportProvider (V-Sprint 1)             │
│  ├── WordImportProvider (future)                │
│  └── GoogleDocsImportProvider (future)          │
│                                                 │
│  INativeEditorService (future)                  │
│  IClientWriteService — iOS / Android (future)   │
└─────────────────────────────────────────────────┘
              │  all write to
              ▼
PLATFORM LAYER
┌─────────────────────────────────────────────────┐
│  Project → Section (working state)              │
│         → SectionVersion (published state)      │
│         → Comments, ReadEvents, Notifications   │
│         → Reader experience                     │
└─────────────────────────────────────────────────┘
```

### 3.1 Ingestion Layer

The ingestion layer gets content into the platform layer. It has no influence over publishing, versioning, or the reader experience. All ingestion channels — sync or import — write to the same `Section` tree via `Section.HtmlContent`. The platform layer does not know or care which channel wrote a given section's content.

Two categories of ingestion channel exist:

**Sync providers** (`ISyncProvider`) — continuous, system-driven ingestion. The system polls or is triggered externally. The author does not initiate individual sync events. `ScrivenerSyncService` is the current implementation.

**Import providers** (`IImportProvider`) — one-shot, author-initiated ingestion. The author selects a file and uploads it. The provider converts the file to HTML and writes to `Section.HtmlContent`. No polling, no external connection required.

Current ingestion channels:
- **Scrivener via Dropbox sync** — `ScrivenerSyncService` implements `ISyncProvider`
- **RTF manual upload** — `RtfImportProvider` implements `IImportProvider` (V-Sprint 1)

Future ingestion channels:
- Word / docx import — `WordImportProvider`
- Google Docs import — `GoogleDocsImportProvider`
- Native DraftView editor
- iOS / Android client (post-revenue)

### 3.2 Platform Layer

The platform layer is DraftView's product. It is source-agnostic. It manages the `Project` and `Section` tree, `SectionVersion` records, comments, read events, notifications, reader access and experience, versioning and publishing behaviour.

### 3.3 Sync Provider Abstraction

Sync is provider-agnostic via `ISyncProvider`. The application layer coordinates sync without knowing which provider is in use. `ScrivenerSyncService` is the current concrete implementation.

**Key sync invariant:**
> Sync never creates versions. Sync only updates working state (`Section.HtmlContent`). The author creates versions. Always.

**Key import invariant:**
> Import never creates versions. Import writes to working state (`Section.HtmlContent`). The author creates versions. Always.

Both invariants are identical in effect. Neither ingestion category has any publishing authority.

### 3.4 Import Provider Abstraction

```csharp
// Application layer
public interface IImportProvider
{
    string SupportedExtension { get; }   // e.g. ".rtf", ".docx"
    string ProviderName { get; }         // e.g. "RTF", "Word"

    Task<string> ConvertToHtmlAsync(
        Stream fileStream,
        CancellationToken cancellationToken = default);
}
```

The import provider converts a file stream to HTML. It knows nothing about sections, projects, or authors. The application service (`ImportService`) owns the write to `Section.HtmlContent`.

### 3.5 Section Tree Management

Manual upload requires section creation without a Scrivener UUID. All manual section creation goes through `SectionTreeService.GetOrCreateForUpload`. This is the single guarded creation point for author-managed sections.

When the explicit tree builder UI (Option A) is built post-launch, only `SectionTreeService` changes. Nothing above it is touched.

### 3.6 Future: Razor Class Library Extraction

If a second sync provider is ever built, Scrivener-specific code (`ScrivenerSyncService`, `ScrivenerProjectDiscoveryService`, `IScrivenerProjectParser`, `DropboxController`, sync views) should be extracted into `DraftView.Sync.Scrivener` as a Razor Class Library, registered via `builder.AddScrivenerSync()`. This is deferred until a second provider is genuinely needed — YAGNI applies.

---

## 4. Domain Entity Model

### 4.1 Project

Represents an author's managed work. Exists independently of any sync source.

| Property | Description |
|----------|-------------|
| `Id` | Unique identifier |
| `AuthorId` | Tenancy-agnostic owner |
| `Name` | Project title |
| `ProjectType` | Enum: `ScrivenerDropbox`, `Manual`. Determines which ingestion channels are available |
| `DropboxPath` | Scrivener-specific sync path. Null when `ProjectType = Manual`. Moves to sync config in future extraction |
| `SyncRootId` | Stable external root identity used by sync reconciliation. Null when `ProjectType = Manual` |
| `IsReaderActive` | Whether readers can currently access this project |
| `SyncStatus` | `Healthy`, `Stale`, `Error`, `Syncing`. Null when `ProjectType = Manual` |
| `LastSyncedAt` | Timestamp of most recent successful sync. Null when `ProjectType = Manual` |
| `SyncErrorMessage` | Populated when `SyncStatus = Error`. Null when `ProjectType = Manual` |

**`ProjectType` enum:**

| Value | Description |
|-------|-------------|
| `ScrivenerDropbox` | Project synced via Dropbox / Scrivener pipeline. Dropbox fields required |
| `Manual` | Author-managed project. No sync connection. All content via import or future native editor |

### 4.2 Section

Represents a node in the author's content tree. Source-agnostic.

| Property | Description |
|----------|-------------|
| `Id` | Unique identifier |
| `ProjectId` | FK to Project |
| `ParentId` | FK to Section. Null for root nodes |
| `Title` | Display title |
| `SortOrder` | Order among siblings |
| `NodeType` | `Folder` or `Document` |
| `HtmlContent` | **Working state prose.** Written by any ingestion channel. Null for Folder nodes |
| `ContentHash` | Hash of current `HtmlContent`. Used to detect change since last publish |
| `IsPublished` | True when at least one `SectionVersion` exists and is reader-visible |
| `PublishedAt` | Timestamp of most recent publish action |
| `ContentChangedSincePublish` | True when `ContentHash` differs from hash at last publish. Advisory |
| `ScrivenerUuid` | Scrivener-specific reconciliation key. Null for manually-created sections. Known debt — moves to sync mapping in future extraction sprint |
| `ScrivenerStatus` | Display-only. Scrivener status label. Null for manually-created sections. Never used as a business rule gate |

**Section creation rules:**
- Sections created by sync: `ScrivenerUuid` populated, reconciled against existing records
- Sections created by manual upload: `ScrivenerUuid` null, created via `SectionTreeService.GetOrCreateForUpload`
- `ScrivenerUuid` being null is never an error — it is the expected state for manually-managed sections

### 4.3 SectionVersion

An immutable snapshot of a `Section`'s content at the moment of a Republish action. This is the published state. Readers always see the latest `SectionVersion`.

| Property | Description |
|----------|-------------|
| `Id` | Unique identifier |
| `SectionId` | FK to Section |
| `AuthorId` | Tenancy-agnostic owner |
| `VersionNumber` | 1-based integer. Scoped per `SectionId`. Never reused |
| `HtmlContent` | Immutable snapshot of prose at publish time |
| `ContentHash` | Hash of `HtmlContent` at publish time. Used for diff in later sprints |
| `ChangeClassification` | Nullable enum: `Polish`, `Revision`, `Rewrite`. Populated from V-Sprint 4 |
| `AiSummary` | Nullable string. One-line summary. Populated from V-Sprint 5 |
| `CreatedAt` | Timestamp of version creation |

### 4.4 Comment (updated)

| Property | Description |
|----------|-------------|
| `Id` | Unique identifier |
| `SectionId` | FK to Section. The stable anchor. Always present |
| `SectionVersionId` | FK to SectionVersion. Nullable. The version the comment was made against. Set at creation; never updated |
| `AuthorAccountId` | FK to Account |
| `ParentCommentId` | FK to Comment. Null for root comments |
| `Body` | Comment text |
| `Visibility` | `Public` or `Private`. Immutable after creation |
| `CreatedAt` | Timestamp of creation |
| `EditedAt` | Timestamp of most recent edit. Null if never edited |
| `IsSoftDeleted` | Soft-delete flag |
| `SoftDeletedAt` | Timestamp of soft deletion |

**Comment version anchoring rules:**
- `SectionVersionId` is set to the current latest `SectionVersion.Id` at comment creation time
- If no version exists yet (pre-versioning section), `SectionVersionId` is null
- Comments are never hidden when versions change — only labelled
- Label: "made on version N" shown when `SectionVersionId` does not match the current version

### 4.5 ReadEvent (updated)

One record per reader per section.

| Property | Description |
|----------|-------------|
| `Id` | Unique identifier |
| `SectionId` | FK to Section |
| `UserId` | FK to User |
| `FirstOpenedAt` | Immutable after creation |
| `LastOpenedAt` | Updated on each open |
| `OpenCount` | Number of times opened |
| `LastReadVersionNumber` | `VersionNumber` of the `SectionVersion` most recently read. Nullable. Drives update messaging |
| `ScrollPosition` | Nullable int. Pixel offset for Kindle-style resume |

---

## 5. Data State Model

**Working State** — `Section.HtmlContent`
- Written by any ingestion channel (sync or import)
- Not visible to readers
- The source of truth for what the author is currently writing

**Published State** — latest `SectionVersion.HtmlContent`
- Immutable
- Reader-facing
- Created only via Republish action
- Independent of which ingestion channel last updated the working state

---

## 6. Version Unit

> `NodeType.Document` is the version unit. Always.

A `Document` may be:
- A standalone chapter (Author A — one Document per chapter)
- A scene within a chapter (Author B — multiple Documents inside a Folder)
- A manually uploaded file (Author C — no Scrivener, files uploaded directly)

All three are versioned identically. The platform does not distinguish between these author types at the domain level.

The word "scene" does not appear in DraftView's UI or domain vocabulary. DraftView uses the author's own titles.

**Version Number Scope:** `VersionNumber` is 1-based, scoped per `SectionId`, never reused. Implementation uses `MAX(VersionNumber) + 1` at publish time.

---

## 7. Publishing Rules

- Republish is the only mechanism for version creation. Never automatic.
- Sync never creates versions.
- Import never creates versions.
- A `Document` section is publishable when: `NodeType = Document`, `IsSoftDeleted = false`, `HtmlContent` is not null or empty.
- `ScrivenerStatus` is never used as a publishability gate. Null status is always publishable.
- Chapter-level Republish is a batch operation creating one `SectionVersion` per non-soft-deleted `Document` descendant.

---

## 8. Revoke Behaviour

- Authors can revoke the latest version only.
- Rolls back to the previous version; that version becomes reader-visible.
- If the only remaining version is revoked: `Section.IsPublished = false`, readers can no longer access the section.
- Revoke is not available when only one version exists and it is the current published version — the author must use Unpublish instead.

---

## 9. Reader Model

- Readers always see the latest `SectionVersion.HtmlContent`.
- Fallback to `Section.HtmlContent` for pre-versioning published sections (temporary, removed once all sections have been republished at least once).
- Readers cannot browse or compare versions.
- `ReadEvent.LastReadVersionNumber` drives update messaging.

### 9.1 Update Messaging

| State | Message |
|-------|---------|
| Reader has not yet opened this section | No message |
| Reader has opened it and a newer version exists | "Updated since you last read" |
| Reader is on the latest version | No message |

### 9.2 Update Banner

Non-blocking top banner. Shows version number and one-line AI summary (from V-Sprint 5). Dismissible. Shown once per version per reader. Version label clickable to reopen section.

---

## 10. Manual Upload Flow

An author with `ProjectType = Manual` — or any author who chooses to upload a file rather than sync — uses the manual upload path.

### 10.1 Creating a Section via Upload

1. Author navigates to their project's Sections view
2. Author clicks **Add Section** or **Upload Draft** against an existing section
3. **Add Section** calls `SectionTreeService.GetOrCreateForUpload(projectId, title, parentId, sortOrder)`
   - Finds an existing section by title + parent within the project, or creates one
   - Created sections have `ScrivenerUuid = null`, `NodeType = Document`
   - `SortOrder` defaults to end of sibling list
4. File picker accepts `.rtf` (v1); `.docx` added in a subsequent task
5. File stream passed to `IImportProvider` implementation selected by file extension
6. Resulting HTML written to `Section.HtmlContent` by `ImportService`
7. `ContentHash` updated; `ContentChangedSincePublish` set if a version exists
8. UI shows **Draft updated** indicator on the section row

The author then republishes in the normal way to create a version and make content visible to readers.

### 10.2 Manual Project Creation

An author creating a `Manual` project:
- Provides project name only — no Dropbox path or sync configuration required
- Can immediately begin adding sections and uploading content
- Can later add sections as folders to organise their tree (using `SectionTreeService`)
- Sync-related UI elements (sync status, Dropbox connect) are hidden for `Manual` projects

### 10.3 Tree Management (Current — Option B)

The section tree for manual projects is built implicitly through uploads. The author provides a title and optional parent on each upload. `SectionTreeService.GetOrCreateForUpload` is the single creation gate.

**Option A (post-launch):** An explicit drag-and-drop tree builder UI will replace the implicit creation flow. Only `SectionTreeService` changes at that point — nothing above it.

---

## 11. Services

### 11.1 `SectionTreeService` (new — V-Sprint 1)

Single responsibility: managing the `Section` tree structure for author-managed projects.

```csharp
// The only method that creates a Section without a ScrivenerUuid
GetOrCreateForUpload(int projectId, string title, int? parentId, int? sortOrder)
    → Section

// Powers the upload parent dropdown and the future tree builder
GetTree(int projectId)
    → IReadOnlyList<SectionTreeNode>
```

`GetOrCreateForUpload` is the **only** place in the codebase where a `Section` with `ScrivenerUuid = null` is ever created. This constraint is enforced at code review and by the absence of any other creation path.

### 11.2 `ImportService` (new — V-Sprint 1)

Orchestrates the manual upload flow. Knows about sections and projects; delegates conversion to `IImportProvider`.

```csharp
Task ImportAsync(
    int projectId,
    int sectionId,
    Stream fileStream,
    string fileName,
    int authorId,
    CancellationToken cancellationToken = default)
```

Behaviour:
1. Resolves the correct `IImportProvider` by file extension
2. Calls `provider.ConvertToHtmlAsync(fileStream)`
3. Writes resulting HTML to `Section.HtmlContent`
4. Recomputes `ContentHash`
5. Sets `ContentChangedSincePublish = true` if a `SectionVersion` exists for this section
6. Saves via unit of work

### 11.3 `VersioningService` (new — V-Sprint 1)

```csharp
Task RepublishChapterAsync(int chapterId, int authorId, CancellationToken ct)
Task RepublishSectionAsync(int sectionId, int authorId, CancellationToken ct)  // V-Sprint 6
Task RevokeLatestVersionAsync(int sectionId, int authorId, CancellationToken ct)  // V-Sprint 1 Phase 4+
```

### 11.4 `ISyncProvider` (existing, formalised)

```csharp
public interface ISyncProvider
{
    Task SyncProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<bool> IsAvailableAsync(int projectId, CancellationToken cancellationToken = default);
}
```

`ScrivenerSyncService` implements this interface. Sync writes to `Section.HtmlContent`. Never creates versions.

---

## 12. System Behaviour Summary

| Event | Action |
|-------|--------|
| Sync runs | `Section.HtmlContent` updated. No version created |
| File imported | `Section.HtmlContent` updated. No version created |
| Content changes | `ContentChangedSincePublish` set true |
| Republish | `SectionVersion` created. Reader sees new version. `Comment.SectionVersionId` set on new comments |
| Revoke | Latest `SectionVersion` soft-removed. Previous version becomes reader-visible |
| Lock active | Republish blocked (V-Sprint 7) |
| Schedule active | Republish suggestion delayed. Action always available (V-Sprint 7) |
| Reader opens section | Latest `SectionVersion.HtmlContent` served. `ReadEvent` updated. `LastReadVersionNumber` set |

---

## 13. Key Constraints

- Republish is the only way to create versions
- Sync never creates versions
- Import never creates versions
- `NodeType.Document` is the version unit
- `NodeType.Folder` is a publishing container and batch tool
- `ScrivenerStatus` is display-only — never a business rule gate
- `ScrivenerUuid = null` is valid and expected for manually-managed sections
- Version numbers are per-section, 1-based, never reused
- Pending change indicator is advisory only
- Scheduling never blocks republish
- Locking blocks publishing only
- No inline diff preview in AI summaries
- `SectionTreeService.GetOrCreateForUpload` is the only path for manual section creation
- `IImportProvider` converts only — it never writes to the database

---

## 14. Known Debt

- `Section.ScrivenerUuid` — moves off `Section` into a sync mapping table in the sync extraction sprint
- `Section.ScrivenerStatus` + `UpdateScrivenerStatus()` — Scrivener display metadata, name is honest
- `Project.DropboxPath`, `Project.SyncRootId`, sync status fields — move to sync-specific config in the extraction sprint
- `DraftView.Sync.Scrivener` RCL extraction — deferred until a second sync provider is built
- `.docx` import support (`WordImportProvider`) — deferred, added as a separate task after V-Sprint 1

---

---

# V-Sprint 1 — Core Versioning Backbone + Manual Upload

## Goal

Prove two things in parallel:
1. Working state → Republish → Version → Reader sees latest version
2. Author with no Scrivener → Create project → Upload file → Republish → Reader sees content

Both flows share the same versioning infrastructure. Manual upload is not a separate sprint — it is a first-class delivery within V-Sprint 1.

## Phased Delivery Rule

> Each phase must be independently deployable to production.
> No phase leaves the system in a broken or partially visible state.
> Tests must be green before moving to the next phase.

---

## Phase 1 — Domain + Infrastructure Foundation

**Goal:** Establish the versioning data model and manual upload domain model. No behaviour change visible in production.

### Domain (TDD required)

**`SectionVersion` entity:**
- `Id`, `SectionId`, `AuthorId`, `VersionNumber`, `HtmlContent`, `ContentHash`, `ChangeClassification` (nullable), `AiSummary` (nullable), `CreatedAt`
- Factory: `SectionVersion.Create(section, authorId)` — snapshots `HtmlContent` and `ContentHash`, assigns next `VersionNumber`
- Invariants: only `NodeType.Document`; not soft-deleted; `HtmlContent` not null or empty

**`ReadEvent` additions:**
- `LastReadVersionNumber` — nullable int
- `UpdateLastReadVersion(int versionNumber)` domain method

**`Comment` addition:**
- `SectionVersionId` — nullable FK to `SectionVersion`

**`Project` addition:**
- `ProjectType` enum property: `ScrivenerDropbox | Manual`

**`Section` addition:**
- `ScrivenerUuid = null` explicitly valid. No invariant violation. No nullable guard warnings.

### Domain Tests (write failing first)

`SectionVersion`:
- `Create_WithDocumentSection_CreatesVersionWithSnapshot`
- `Create_WithFolderSection_ThrowsInvariantViolation`
- `Create_WithSoftDeletedSection_ThrowsInvariantViolation`
- `Create_WithNullHtmlContent_ThrowsInvariantViolation`
- `Create_AssignsCorrectVersionNumber`
- `Create_AssignsVersionNumberOne_WhenFirstVersion`

`ReadEvent`:
- `UpdateLastReadVersion_SetsVersionNumber`
- `UpdateLastReadVersion_OverwritesPreviousValue`

`Project`:
- `Create_WithManualType_HasNullDropboxPath`
- `Create_WithScrivenerDropboxType_RequiresDropboxPath`

### Infrastructure

- `ISectionVersionRepository`
  - `GetLatestAsync(int sectionId, CancellationToken ct)`
  - `GetAllBySectionIdAsync(int sectionId, CancellationToken ct)`
  - `AddAsync(SectionVersion version, CancellationToken ct)`
- `SectionVersionRepository` EF implementation
- `SectionVersionConfiguration` EF config
- Register in `DraftViewDbContext` and DI
- Migration: `AddVersioningAndManualUpload`
  - `SectionVersions` table
  - `LastReadVersionNumber` nullable int on `ReadEvents`
  - `SectionVersionId` nullable FK on `Comments`
  - `ProjectType` int column on `Projects` (default `0` = `ScrivenerDropbox`)

### Deployable state
- Migration runs cleanly on production
- No existing behaviour changes
- All existing tests remain green

---

## Phase 2 — Section Tree Service + Import Provider

**Goal:** Manual section creation and file import are wired and testable. Still no UI.

### Application (TDD required)

**`SectionTreeService`:**
- `GetOrCreateForUpload(int projectId, string title, int? parentId, int? sortOrder) → Section`
- `GetTree(int projectId) → IReadOnlyList<SectionTreeNode>`

**`IImportProvider` interface + `RtfImportProvider`:**
- `SupportedExtension` → `".rtf"`
- `ProviderName` → `"RTF"`
- `ConvertToHtmlAsync(Stream, CancellationToken)` — delegates to existing `RtfConverter`

**`ImportService`:**
- `ImportAsync(projectId, sectionId, fileStream, fileName, authorId, ct)`
- Resolves provider by extension; throws `UnsupportedFileTypeException` for unknown extensions
- Writes to `Section.HtmlContent`; updates `ContentHash`; sets `ContentChangedSincePublish` if version exists

### Application Tests (write failing first)

`SectionTreeService`:
- `GetOrCreateForUpload_CreatesSection_WhenNoneExists`
- `GetOrCreateForUpload_ReturnsExisting_WhenTitleAndParentMatch`
- `GetOrCreateForUpload_NeverCreatesDuplicate`
- `GetOrCreateForUpload_CreatedSection_HasNullScrivenerUuid`
- `GetOrCreateForUpload_CreatedSection_IsDocument`
- `GetOrCreateForUpload_DefaultsSortOrder_ToEndOfSiblingList`
- `GetTree_ReturnsSortedHierarchy`
- `GetTree_ExcludesSoftDeletedSections`

`ImportService`:
- `ImportAsync_WritesHtmlToSection`
- `ImportAsync_UpdatesContentHash`
- `ImportAsync_SetsDirtyFlag_WhenVersionExists`
- `ImportAsync_DoesNotSetDirtyFlag_WhenNoVersionExists`
- `ImportAsync_Throws_ForUnsupportedExtension`

### Deployable state
- Services wired in DI but no controller calls them yet
- All existing tests remain green

---

## Phase 3 — Versioning Application Layer

**Goal:** `VersioningService` exists and is callable. Still no UI.

### Application (TDD required)

**`IVersioningService`:**
- `RepublishChapterAsync(int chapterId, int authorId, CancellationToken ct)`

**`VersioningService` implementation:**
- Loads chapter (Folder) and validates ownership
- Gets all non-soft-deleted Document descendants
- For each Document: calls `SectionVersion.Create()`, sets `Comment.SectionVersionId` on future comments (not retroactively)
- Persists via `ISectionVersionRepository`
- Sets `Section.IsPublished = true` and `Section.PublishedAt`
- Saves via unit of work

### Application Tests (write failing first)

- `RepublishChapterAsync_WithValidChapter_CreatesVersionPerDocument`
- `RepublishChapterAsync_WithNoDocuments_ThrowsInvariantViolation`
- `RepublishChapterAsync_WithFolderSection_ThrowsInvariantViolation`
- `RepublishChapterAsync_IgnoresSoftDeletedDocuments`
- `RepublishChapterAsync_VersionNumberIncrements`
- `RepublishChapterAsync_WorksForManualProject`

### Deployable state
- Service registered and dormant
- All existing tests remain green

---

## Phase 4 — Reader Content Source

**Goal:** Readers see `SectionVersion.HtmlContent`. Backward-compatible fallback for pre-versioning sections.

### Web

- Update all reader views (Desktop and Mobile) that render `section.HtmlContent`:
  - Resolve content via `ISectionVersionRepository.GetLatestAsync(section.Id)`
  - If a `SectionVersion` exists: render `sectionVersion.HtmlContent`
  - If no `SectionVersion` (pre-versioning section): fall back to `Section.HtmlContent`
- Update `ReadEvent` recording: set `LastReadVersionNumber` when a version exists
- Set `Comment.SectionVersionId` to current latest version Id at comment creation time (null if no version yet)

### Deployable state
- Existing readers see identical content — fallback covers all currently published sections
- New comments are version-anchored going forward
- All existing tests remain green

---

## Phase 5 — Author Republish + Manual Upload UI

**Goal:** Both flows visible and functional. Full end-to-end for both sync and manual authors.

### Web — Republish

- Add **Republish** button to `Author/Sections` view
  - Shown for published Folder (chapter) sections
  - POST to `AuthorController.RepublishChapter(int chapterId, int projectId)`
- `RepublishChapter` action calls `IVersioningService.RepublishChapterAsync`
- Toast success + redirect. Graceful toast error on failure
- No inline styles; CSS classes only

### Web — Manual Upload

- **Create Manual Project** option on project creation flow
  - `ProjectType = Manual` projects skip Dropbox configuration entirely
  - No sync status UI shown for manual projects
- **Upload Draft** button on each Document row in Sections view
  - File picker (`.rtf` accepted; other extensions rejected with clear error message)
  - POST to `AuthorController.UploadDraft(int sectionId, int projectId, IFormFile file)`
  - Calls `SectionTreeService` + `ImportService`
  - Shows **Draft updated** toast on success
- **Add Section** button on Sections view for manual projects
  - Modal: title field + optional parent dropdown (powered by `SectionTreeService.GetTree`)
  - POST to `AuthorController.AddSection(int projectId, string title, int? parentId)`
  - Returns updated section row partial

### Deployable state
- Author (sync or manual) can Republish and create a version
- Reader immediately sees new version content
- Manual author can create a project, add sections, upload RTF, and republish
- Full end-to-end working for both author types
- All existing tests remain green
- Manual browser verification complete for both flows

---

## Do NOT include in V-Sprint 1

- AI summaries
- Diff highlighting
- Change classification
- Per-document (scene-level) publishing UI
- Scheduling or locking
- Dedicated Publishing Page
- Revoke action
- Reader banner or update messaging
- Version retention limits
- `.docx` import

---

---

# V-Sprint 2 — Diff and Highlighting

## Goal

Deliver the core differentiator: readers see what changed.

## Phases

**Phase 1 — Diff Engine (Domain)**
- Paragraph-level diff between two `HtmlContent` strings
- Output: added, removed, unchanged paragraphs
- Works for both sync-sourced and import-sourced content (source is irrelevant)
- TDD throughout

**Phase 2 — Application Diff Service**
- `IDiffService.ComputeDiff(string from, string to)`
- Compare reader's `LastReadVersionNumber` version against current latest

**Phase 3 — Reader Highlighting**
- Highlight changed paragraphs in reader view
- Always-on in this sprint
- Update `LastReadVersionNumber` on open

---

# V-Sprint 3 — Reader Experience Layer

## Goal

Make the system usable and intentional for readers.

## Phases

**Phase 1 — Reader State**
- Update `LastReadVersionNumber` on section open
- Add `LastReadAt` to `ReadEvent`

**Phase 2 — Messaging**
- "Updated since you last read" — reader has read it but a newer version exists
- No message on first read or when current

**Phase 3 — Update Banner**
- Non-blocking top banner
- Dismissible
- Shown once per version per reader
- Version label clickable to reopen

---

# V-Sprint 4 — Pending Change Indicator and Classification

## Goal

Give authors visibility into the nature of changes before publishing.

## Phases

**Phase 1 — Change Classification Domain**
- `ChangeClassification` enum: `Polish`, `Revision`, `Rewrite`
- Diff-based heuristic classification

**Phase 2 — Classification Service**
- Evaluate `Section.HtmlContent` vs latest `SectionVersion.HtmlContent`
- `IChangeClassificationService`
- Works identically for sync-sourced and import-sourced working state

**Phase 3 — Author UI Indicator**
- Indicator next to Republish button on Sections view
- Colour and label only

---

# V-Sprint 5 — AI Summary System

## Goal

Add monetisation-relevant value. AI summaries that name characters, locations, and events from the prose — not generic diffs.

## Phases

**Phase 1 — AI Summary Service**
- `IAiSummaryService`
- Compares previous `SectionVersion.HtmlContent` against current `Section.HtmlContent`
- Prompt instructs: name characters and locations from the prose; never write generic phrases; 2–4 sentences; author's voice as a note to beta readers
- One-line summary (always available); full summary (tier-gated)
- For first-version sections: summarise what the section introduces, not what changed

**Phase 2 — Publish Flow Integration**
- Show AI summary in editable textarea on Republish confirmation step
- Author edits freely before confirming
- AI failure degrades to empty textarea — never blocks publication

**Phase 3 — Reader Banner Summary**
- Reader banner shows one-line `AiSummary` from current `SectionVersion`

---

# V-Sprint 6 — Per-Document Publishing

## Goal

Enable granular publishing for authors who compose in scenes — and for manual authors uploading individual documents.

## Phases

**Phase 1 — Per-Document Application**
- `RepublishSectionAsync(int sectionId, int authorId)` on `IVersioningService`
- Available for both sync-sourced and import-sourced Documents

**Phase 2 — Publishing Page**
- Dedicated Publishing Page replaces Republish button on Sections view
- Chapter view: Republish and Revoke per chapter
- Expand chapter → per-document controls shown when chapter contains multiple documents
- Manual projects: per-document controls always shown (no chapter grouping assumed)

---

# V-Sprint 7 — Scheduling and Locking

## Phases

**Phase 1 — Chapter Locking**
- Lock blocks all publish actions on the chapter
- Reader sees: "Author is revising this chapter"

**Phase 2 — Scheduling**
- Per chapter (default), optional per document
- Suppresses suggestions only — never blocks republish action

---

# V-Sprint 8 — Dropbox Incremental Sync

## Goal

Scalable and efficient ingestion. No impact on publishing or manual upload.

## Phases

**Phase 1 — Cursor-based sync**
- Only download changed files
- Update `Section.HtmlContent` only — identical to current sync behaviour
- No version creation

---

# V-Sprint 9 — Version Retention and Deletion

## Goal

Enforce the pricing model.

## Phases

**Phase 1 — Retention Domain**
- Version retention rules per tier
- `SectionVersion` permanent deletion (the only case of physical deletion in the system)

**Phase 2 — Enforcement**
- Check limit before creating new version
- Prompt author to delete when limit reached

**Phase 3 — Version Management UI**
- Version list on Publishing Page
- Controlled deletion flow with confirmation

---

# V-Sprint 10 — Tree Builder UI (Option A)

## Goal

Replace implicit section creation (Option B) with an explicit drag-and-drop tree management UI. Targeted at manual-project authors and power users.

## Phases

**Phase 1 — Tree Service Extension**
- `SectionTreeService.CreateSection(projectId, title, parentId, sortOrder, authorId)`
- `SectionTreeService.MoveSection(sectionId, newParentId, newSortOrder, authorId)`
- `SectionTreeService.DeleteSection(sectionId, authorId)` — soft delete

**Phase 2 — Tree Builder UI**
- Drag-and-drop section tree on Project view
- Create, rename, reorder, nest sections
- Upload Draft integrated per-section row

**Phase 3 — Sync Project Tree Display**
- For `ScrivenerDropbox` projects: tree is read-only (controlled by Scrivener)
- Structural changes shown as incoming sync updates

---

# Sprint Order Summary

| Sprint | Goal |
|--------|------|
| V-Sprint 1 | Core versioning backbone + manual upload — 5 deployable phases |
| V-Sprint 2 | Diff and highlighting |
| V-Sprint 3 | Reader experience layer |
| V-Sprint 4 | Pending change indicator and classification |
| V-Sprint 5 | AI summaries |
| V-Sprint 6 | Per-document publishing |
| V-Sprint 7 | Scheduling and locking |
| V-Sprint 8 | Incremental sync |
| V-Sprint 9 | Version retention |
| V-Sprint 10 | Tree builder UI (Option A) |

---

# Phased Delivery Rules

Every phase across every sprint must satisfy all of the following before deployment:

1. All new tests are green
2. No existing tests have regressed
3. The relevant view has been manually verified in the browser
4. Changes are committed to GitHub with a meaningful message
5. TASKS.md is updated
6. The system is in a complete, non-broken state — no half-built features visible to authors or readers

> Build thin, complete slices.
> Validate behaviour early.
> Never build ahead of proof.
> The ingestion channel is irrelevant to the reader.
> The author decides what becomes a version. Always.
> DraftView works without Scrivener.