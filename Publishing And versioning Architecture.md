# DraftView Platform Architecture — Publishing and Versioning (v4.0)

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

---

## 3. Two-Layer Architecture

DraftView is structured as two distinct layers. Nothing from the ingestion layer leaks into the platform layer.

```
INGESTION LAYER
┌─────────────────────────────────────────────────┐
│  ISyncProvider implementations                  │
│  ├── ScrivenerSyncService (current)             │
│  └── [any future provider]                      │
│                                                 │
│  IImportProvider implementations (future)       │
│  ├── GoogleDocsImportProvider                   │
│  └── WordImportProvider                         │
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

The ingestion layer gets content into the platform layer. It has no influence over publishing, versioning, or the reader experience. All ingestion channels write to the same `Section` tree. The platform layer does not know or care which channel wrote a given `Section.HtmlContent`.

Current ingestion channels:
- **Scrivener via Dropbox sync** — `ScrivenerSyncService`. Downloads files, parses `.scrivx` binder and RTF content, writes `HtmlContent` to `Section` records.

Future ingestion channels:
- File import — Google Docs, Word, plain text
- Native DraftView editor
- iOS / Android client (post-revenue)

### 3.2 Platform Layer

The platform layer is DraftView's product. It is source-agnostic. It manages the `Project` and `Section` tree, `SectionVersion` records, comments, read events, notifications, reader access and experience, versioning and publishing behaviour.

### 3.3 Sync Provider Abstraction

Sync is provider-agnostic via `ISyncProvider`. The application layer coordinates sync without knowing which provider is in use. `ScrivenerSyncService` is the current concrete implementation.

**Key sync invariant:**
> Sync never creates versions. Sync only updates working state (`Section.HtmlContent`). The author creates versions. Always.

### 3.4 Future: Razor Class Library Extraction

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
| `DropboxPath` | Scrivener-specific sync path (moves to sync config in future extraction) |
| `SyncRootId` | Stable external root identity used by sync reconciliation |
| `IsReaderActive` | Whether readers can currently access this project |
| `SyncStatus` | `Healthy`, `Stale`, `Error`, `Syncing` |
| `LastSyncedAt` | Timestamp of most recent successful sync |
| `SyncErrorMessage` | Populated when `SyncStatus = Error` |

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
| `HtmlContent` | **Working state prose.** Updated by any ingestion channel. Null for Folder nodes |
| `ContentHash` | Hash of current `HtmlContent`. Used to detect change since last publish |
| `IsPublished` | True when at least one `SectionVersion` exists and is reader-visible |
| `PublishedAt` | Timestamp of most recent publish action |
| `ContentChangedSincePublish` | True when `ContentHash` differs from hash at last publish. Advisory |
| `ScrivenerUuid` | Scrivener-specific reconciliation key. Known debt — moves to sync mapping in future extraction sprint |
| `ScrivenerStatus` | Display-only. Scrivener status label. Never used as a business rule gate |

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

### 4.4 ReadEvent (updated)

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
- Continuously updated by any ingestion channel
- Not visible to readers
- The source of truth for what the author is currently writing

**Published State** — latest `SectionVersion.HtmlContent`
- Immutable
- Reader-facing
- Created only via Republish action

---

## 6. Version Unit

> `NodeType.Document` is the version unit. Always.

A `Document` may be:
- A standalone chapter (Author A — the author's entire chapter is one Document)
- A scene within a chapter (Author B — the author composes multiple Documents inside a Folder)

Both are versioned identically. The platform does not distinguish between these author types at the domain level. The UI adapts based on tree structure.

The word "scene" does not appear in DraftView's UI or domain vocabulary. DraftView uses the author's own titles without labelling them as scenes.

**Version Number Scope:** `VersionNumber` is 1-based, scoped per `SectionId`, never reused. Implementation uses `MAX(VersionNumber) + 1` at publish time.

---

## 7. Publishing Rules

- Republish is the only mechanism for version creation. Never automatic.
- Sync never creates versions.
- A `Document` section is publishable when: `NodeType = Document`, `IsSoftDeleted = false`, `HtmlContent` is not null or empty.
- `ScrivenerStatus` is never used as a publishability gate. Null status is always publishable.
- Chapter-level Republish is a batch operation creating one `SectionVersion` per non-soft-deleted `Document` descendant.

---

## 8. Revoke Behaviour

- Authors can revoke the latest version only.
- Rolls back to previous version, updates reader-visible state.
- If the only remaining version is revoked: `Section.IsPublished = false`, readers can no longer access the section.

---

## 9. Reader Model

- Readers always see the latest `SectionVersion.HtmlContent`.
- Fallback to `Section.HtmlContent` for pre-versioning published sections (temporary, removed once all sections have been republished).
- Readers cannot browse or compare versions.
- `ReadEvent.LastReadVersionNumber` drives update messaging.

### 9.1 Update Messaging

| State | Message |
|-------|---------|
| Reader has not yet read this section | "Updated since you last read" |
| Reader has read it but a newer version exists | "Updated from the last version" |
| First read | No message |

### 9.2 Update Banner

Non-blocking top banner. Shows version number and one-line summary (from V-Sprint 5). Dismissible. Shown once per version per reader. Version label clickable to reopen.

---

## 10. System Behaviour Summary

| Event | Action |
|-------|--------|
| Sync runs | `Section.HtmlContent` updated. No version created |
| Content changes | `ContentChangedSincePublish` set true |
| Republish | `SectionVersion` created. Reader sees new version |
| Revoke | Latest `SectionVersion` removed. Previous version becomes reader-visible |
| Lock active | Republish blocked (V-Sprint 7) |
| Schedule active | Republish suggestion delayed. Action always available (V-Sprint 7) |
| Reader opens section | Latest `SectionVersion.HtmlContent` served. `ReadEvent` updated |

---

## 11. Key Constraints

- Republish is the only way to create versions
- Sync never creates versions
- `NodeType.Document` is the version unit
- `NodeType.Folder` is a publishing container and batch tool
- `ScrivenerStatus` is display-only — never a business rule gate
- Version numbers are per-section, 1-based, never reused
- Pending change indicator is advisory only
- Scheduling never blocks republish
- Locking blocks publishing only
- No inline diff preview in AI summaries

---

## 12. Known Debt

- `Section.ScrivenerUuid` — moves off `Section` into a sync mapping table in the sync extraction sprint
- `Section.ScrivenerStatus` + `UpdateScrivenerStatus()` — Scrivener display metadata, name is honest
- `Project.DropboxPath` — moves to sync-specific config in the extraction sprint
- `DraftView.Sync.Scrivener` RCL extraction — deferred until a second sync provider is built

---

# V-Sprint 1 — Core Versioning Backbone

## Goal

Prove: Working state → Republish → Version → Reader sees latest version

## Phased Delivery Rule

> Each phase must be independently deployable to production.
> No phase leaves the system in a broken or partially visible state.
> Tests must be green before moving to the next phase.

---

## Phase 1 — Domain + Infrastructure Foundation

**Goal:** Establish the versioning data model. No behaviour change in production.

### Domain (TDD required)
- `SectionVersion` entity
  - `Id`, `SectionId`, `AuthorId`, `VersionNumber`, `HtmlContent`, `ContentHash`, `ChangeClassification` (nullable), `AiSummary` (nullable), `CreatedAt`
  - Factory method `SectionVersion.Create(section, authorId)` — snapshots current `HtmlContent` and `ContentHash`, assigns next `VersionNumber`
  - Invariants: only `NodeType.Document` can be versioned; soft-deleted sections cannot be versioned; `HtmlContent` must not be null
- `ReadEvent.LastReadVersionNumber` — nullable int added to existing entity
- `ReadEvent.UpdateLastReadVersion(int versionNumber)` domain method

### Domain Tests (write failing first)
- `Create_WithDocumentSection_CreatesVersionWithSnapshot`
- `Create_WithFolderSection_ThrowsInvariantViolation`
- `Create_WithSoftDeletedSection_ThrowsInvariantViolation`
- `Create_WithNullHtmlContent_ThrowsInvariantViolation`
- `Create_AssignsCorrectVersionNumber`
- `UpdateLastReadVersion_SetsVersionNumber`
- `UpdateLastReadVersion_OverwritesPreviousValue`

### Infrastructure
- `ISectionVersionRepository` interface
  - `GetLatestAsync(Guid sectionId, CancellationToken ct)`
  - `GetAllBySectionIdAsync(Guid sectionId, CancellationToken ct)`
  - `AddAsync(SectionVersion version, CancellationToken ct)`
- `SectionVersionRepository` EF implementation
- `SectionVersionConfiguration` EF configuration
- Register in `DraftViewDbContext` and DI
- Migration: `AddSectionVersioning`
  - new `SectionVersions` table
  - `LastReadVersionNumber` nullable int column on `ReadEvents`

### Deployable state
- Migration runs cleanly on production
- No existing behaviour changes
- All existing tests remain green
- New entity and repository are wired but unused

---

## Phase 2 — Application Layer

**Goal:** Introduce the versioning service. Still no UI change.

### Application (TDD required)
- `IVersioningService` interface
  - `RepublishChapterAsync(Guid chapterId, Guid authorId, CancellationToken ct)`
- `VersioningService` implementation
  - Loads chapter (Folder) and validates ownership
  - Gets all non-soft-deleted Document descendants
  - For each Document: calls `SectionVersion.Create()`, persists via `ISectionVersionRepository`
  - Sets `Section.IsPublished = true` and `Section.PublishedAt` on each versioned Document
  - Saves via `IUnitOfWork`
- Register in DI

### Application Tests (write failing first)
- `RepublishChapterAsync_WithValidChapter_CreatesVersionPerDocument`
- `RepublishChapterAsync_WithNoDocuments_ThrowsInvariantViolation`
- `RepublishChapterAsync_WithFolderSection_ThrowsInvariantViolation`
- `RepublishChapterAsync_IgnoresSoftDeletedDocuments`
- `RepublishChapterAsync_VersionNumberIncrements`

### Deployable state
- Service exists and is registered
- No controller calls it yet — it is wired but dormant
- All existing tests remain green

---

## Phase 3 — Reader Content Source

**Goal:** Readers see `SectionVersion.HtmlContent` instead of `Section.HtmlContent`. Existing readers see no visible difference — fallback ensures backward compatibility.

### Web
- Update all reader views (Desktop and Mobile) that render `section.HtmlContent`:
  - Resolve content via `ISectionVersionRepository.GetLatestAsync(section.Id)`
  - If a `SectionVersion` exists: render `sectionVersion.HtmlContent`
  - If no `SectionVersion` exists (pre-versioning published section): fall back to `Section.HtmlContent`
- Update `ReadEvent` recording to set `LastReadVersionNumber` when a version exists

### Deployable state
- Existing readers see identical content — the fallback covers all currently published sections
- No new UI elements
- All existing tests remain green

---

## Phase 4 — Author Republish Button

**Goal:** Author can create versions. First visible feature.

### Web
- Add **Republish** button to `Author/Sections` view
  - Shown for published Folder (chapter) sections only
  - POST to new `AuthorController.RepublishChapter(Guid chapterId, Guid projectId)` action
- `RepublishChapter` controller action
  - Calls `IVersioningService.RepublishChapterAsync(chapterId, author.Id)`
  - Returns toast success + redirect to Sections view
  - Handles errors gracefully with toast error message
- Style audit: no inline styles, CSS classes only

### Deployable state
- Author can click Republish and create a version
- Readers immediately see the new version content (via Phase 3 resolution)
- Full end-to-end flow working: author republishes → version created → reader sees latest
- All existing tests remain green
- Manual browser verification complete

---

## Do NOT include in V-Sprint 1

- AI summaries
- Diff highlighting
- Change classification
- Pending change indicator
- Per-document (scene-level) publishing
- Scheduling or locking
- Dedicated Publishing Page
- Revoke action
- Reader banner or messaging
- Version retention limits

---

# V-Sprint 2 — Diff and Highlighting

## Goal

Deliver the core differentiator: readers see what changed.

## Phases

**Phase 1 — Diff Engine (Domain)**
- Paragraph-level diff between two `HtmlContent` strings
- Output: added, removed, unchanged paragraphs
- TDD throughout

**Phase 2 — Application Diff Service**
- `IDiffService.ComputeDiff(string from, string to)`
- Compare reader's `LastReadVersionNumber` version against current latest

**Phase 3 — Reader Highlighting**
- Highlight changed paragraphs in reader view
- Always-on in this sprint
- Update `LastReadVersionNumber` on open

## Do NOT include

- AI summaries
- Version classification
- Banner messaging
- Scheduling

---

# V-Sprint 3 — Reader Experience Layer

## Goal

Make the system usable and intentional for readers.

## Phases

**Phase 1 — Reader State**
- Update `LastReadVersionNumber` on section open
- Add `LastReadAt` to `ReadEvent`

**Phase 2 — Messaging**
- "Updated since you last read"
- "Updated from the last version"
- No message on first read

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
- Classification logic (diff-based heuristic)

**Phase 2 — Classification Service**
- Evaluate `Section.HtmlContent` vs latest `SectionVersion.HtmlContent`
- `IChangeClassificationService`

**Phase 3 — Author UI Indicator**
- Indicator next to Republish button on Sections view
- Colour and label only

---

# V-Sprint 5 — AI Summary System

## Goal

Add monetisation-relevant value.

## Phases

**Phase 1 — AI Summary Service**
- `IAiSummaryService`
- One-line summary (always available)
- Full summary (tier-gated)

**Phase 2 — Publish Flow Integration**
- Show summary on Republish confirmation step
- Allow author to edit before confirming

**Phase 3 — Reader Banner Summary**
- Reader banner shows one-line summary

---

# V-Sprint 6 — Per-Document Publishing

## Goal

Enable granular publishing for authors who compose in scenes.

## Phases

**Phase 1 — Per-Document Application**
- `RepublishSectionAsync(Guid sectionId, Guid authorId)` on `IVersioningService`

**Phase 2 — Publishing Page**
- Dedicated Publishing Page replaces Republish button on Sections view
- Chapter view with Republish and Revoke per chapter
- Expand chapter → per-document controls shown only when chapter contains multiple documents

---

# V-Sprint 7 — Scheduling and Locking

## Phases

**Phase 1 — Chapter Locking**
- Lock blocks all publish actions
- Reader sees: "Author is revising this chapter"

**Phase 2 — Scheduling**
- Per chapter (default), optional per document
- Suppresses suggestions only — never blocks republish action

---

# V-Sprint 8 — Dropbox Incremental Sync

## Goal

Scalable and efficient ingestion. No impact on publishing.

## Phases

**Phase 1 — Cursor-based sync**
- Only download changed files
- Update `Section.HtmlContent` only
- No version creation

---

# V-Sprint 9 — Version Retention and Deletion

## Goal

Enforce the pricing model.

## Phases

**Phase 1 — Retention Domain**
- Version retention rules per tier
- `SectionVersion` permanent deletion

**Phase 2 — Enforcement**
- Check limit before creating new version
- Prompt author to delete when limit reached

**Phase 3 — Version Management UI**
- Version list on Publishing Page
- Controlled deletion flow with confirmation

---

# Sprint Order Summary

| Sprint | Goal |
|--------|------|
| V-Sprint 1 | Core versioning backbone — 4 deployable phases |
| V-Sprint 2 | Diff and highlighting |
| V-Sprint 3 | Reader experience layer |
| V-Sprint 4 | Pending change indicator and classification |
| V-Sprint 5 | AI summaries |
| V-Sprint 6 | Per-document publishing |
| V-Sprint 7 | Scheduling and locking |
| V-Sprint 8 | Incremental sync |
| V-Sprint 9 | Version retention |

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