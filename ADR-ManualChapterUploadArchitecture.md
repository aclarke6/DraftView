# ADR — Manual chapter upload architecture

**Date:** 2026-08-18  
**Status:** Accepted

## Context

DraftView already supports Scrivener-synced projects and has an earlier
section-based manual-upload concept. Issue #32 defines a narrower launch
feature: authors upload one `.txt` or `.docx` file per chapter, manual projects
have a flat chapter list, and the system must not infer sub-sections from file
content.

Stage 1 requires a data-model decision before implementation begins.

## Decision

### 1. Keep the source discriminator on the existing `Project` entity

We will keep a single project aggregate and continue to use a project-level
source discriminator rather than introducing parallel `ManualProject` and
`ScrivenerProject` entities.

- Existing project-level source information already gates sync behaviour.
- A shared `Project` keeps author ownership, reader access, notifications, and
  future tenancy work on one path.
- Launch scope does not justify duplicating project lifecycle behaviour across
  two entity types.

For implementation, the current `Project.ProjectType` discriminator is the
source-type gate unless a later cleanup explicitly renames it to
`ProjectSourceType`.

### 2. Model manual uploads as `ManualChapter`, not `Section`

Manual upload will introduce a dedicated `ManualChapter` entity with a flat list
per project. It will not map onto the existing `Section` tree.

Required launch fields:

- `Id`
- `AuthorId`
- `ProjectId`
- `Title`
- `SortOrder`
- `RawContent` (parsed plain text)
- `OriginalFileName`
- `UploadedAt`

This keeps the manual-upload path honest to the author's file structure and
avoids brittle heading-based splitting.

### 3. Store parsed plain text in the database at launch

At launch, `ManualChapter` stores parsed plain text in the database and does not
persist original file bytes or disk-backed chapter files.

- Simpler operational model
- No separate file store to manage
- Easier author-scoped backup, query, and reorder behaviour
- Avoids retaining binary payloads that are not needed by the reader surface

If richer fidelity or file re-download is needed later, raw file storage can be
added as a follow-up design.

### 4. Use explicit `SortOrder`

Chapter order is stored as an explicit integer on `ManualChapter`.

- Stable for drag-to-reorder and accessible up/down fallback
- No dependence on upload timestamp or filename sorting
- Clear persistence model for a flat list

### 5. Support `.txt` and `.docx` only at launch

Launch file support is intentionally narrow:

- `.txt` → plain text parser
- `.docx` → Word parser

### 6. Use `DocumentFormat.OpenXml` for `.docx` parsing

`DocumentFormat.OpenXml` is the chosen `.docx` parsing library.

- Already aligned with the existing .NET ecosystem
- No extra service dependency or opaque wrapper required
- Good fit for extracting plain text without introducing a rich editor model

The parser contract should normalize `.docx` content to plain text with
preserved paragraph breaks; rich formatting is out of scope for v1.

### 7. Launch limits

- **Max file size:** 2 MiB per uploaded chapter file
- **Max chapters per manual project:** 250

These limits comfortably exceed normal prose chapter sizes while protecting the
parser path and database-backed storage model from abuse.

### 8. Support cut/paste as an additional upload method

Authors may also submit chapter content by pasting text directly into a
textarea, in addition to file import. This covers authors who do not work in
`.txt` or `.docx` files and prefer a copy/paste workflow.

- A **Paste content** tab sits alongside the **Upload file** tab in the upload
  modal
- Pasted text is treated as plain text and stored identically to file-parsed
  content
- The author must still provide a chapter title; no filename is recorded for
  paste-origin chapters
- `OriginalFileName` is `null` for paste-origin chapters
- All other invariants (non-empty title, non-null content, valid sort order)
  apply equally

### 9. Provide an inline text editor for minor chapter edits

Authors must be able to make minor corrections to chapter text inside DraftView
without re-uploading a file. An online editor satisfies requirement 4 of issue
#34.

- The editor is activated via an **Edit** button on the chapter row
- It presents the stored `RawContent` in a resizable `<textarea>`
- On save, the existing `ManualChapter` record is updated in place and a new
  `ManualChapterVersion` snapshot is created (see decision 10)
- The editor is not a rich-text editor — plain text only, consistent with the
  v1 content model
- No editor is shown for Scrivener-synced content

### 10. Maintain version history for manual chapters, with hard delete

Version history for manual chapters satisfies requirement 5 of issue #34.

- Every time a chapter's content changes (via file upload, replace, paste, or
  inline edit), the previous content is snapshotted into a new
  `ManualChapterVersion` record
- `ManualChapterVersion` is a separate entity:
  - `Id`
  - `ManualChapterId`
  - `AuthorId`
  - `VersionNumber`
  - `RawContent`
  - `SnapshotReason` (enum: `FileUpload | Replace | PasteUpload | InlineEdit`)
  - `SnapshotAt`
- Version records are immutable after creation — no setter, no update path
- Authors can view a chapter's version list from the chapter row
- **Clear history** removes all version records for that chapter via a
  **hard delete** — this satisfies requirement 5 explicitly and is the one
  permitted physical-delete path in the system
- Hard delete of a `ManualChapterVersion` does not affect the live chapter content
- Clearing history requires explicit confirmation to protect against accidental loss

### 11. Reader transparency — manual and sync projects are indistinguishable

Requirement 2 of issue #34: the reader must not be able to distinguish between
Scrivener-synced and manual-upload projects.

- Reader-facing views (`MobileChapters`, `DesktopRead`, scene/chapter pages)
  render published content regardless of source type
- The publishing pipeline resolves `ManualChapter` content through the same
  `SectionVersion` snapshot mechanism already used by Scrivener sync:
  - Publishing a manual chapter calls `VersioningService.RepublishSectionAsync`
    (or equivalent), creating a `SectionVersion` from `ManualChapter.RawContent`
  - The reader reads `SectionVersion.HtmlContent`, which is source-agnostic
- No reader-facing route, view, or ViewModel exposes `ProjectType` or
  `ManualChapter` identity
- "Manual Upload" labelling is confined to author-only screens

### 12. OOP design principles applied throughout

Requirement 1 of issue #34.

- The parser contract (`IChapterFileParser`) is a polymorphic interface;
  `PlainTextChapterParser` and `DocxChapterParser` are concrete implementations,
  selected by extension without switch statements in the upload handler
- Upload method variants (file vs paste) are unified at the application layer
  via a single `UploadChapterAsync` overload set or a command model —
  controllers dispatch to the same service regardless of input origin
- `ManualChapterVersion` history capture is handled inside the application
  service, not in the controller; the service decides when a snapshot is
  warranted
- Hard-delete of version history is an explicit, named operation
  (`ClearVersionHistoryAsync`) on the application service, not an exposed
  repository call

## Consequences

### Positive

- Keeps manual projects simple and predictable
- Avoids forcing chapter uploads into Scrivener-oriented `Section` semantics
- Preserves a strict separation: sync tree on one path, flat manual chapters on
  another
- Minimizes launch infrastructure by keeping content in the database

### Trade-offs

- Manual projects do not reuse existing `Section` tree tooling
- `.docx` formatting is reduced to normalized plain text
- Future publishing and reader-resolution work must explicitly decide how
  `ManualChapter` participates in reader-facing delivery

## Rejected alternatives

### Separate `ManualProject` and `ScrivenerProject` entities

Rejected because it duplicates project lifecycle and author-scoped behaviour for
little launch value.

### Reusing `Section` for manual chapters

Rejected because this feature explicitly requires chapters to be atomic leaf
items and forbids silent structural inference.

### Storing raw file bytes at launch

Rejected because the launch workflow only needs parsed plain text plus file
metadata, and database simplicity is preferred.
