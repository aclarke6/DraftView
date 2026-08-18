# Task: Manual chapter upload — text and Word documents
**Date:** 2026-08-18
**Branch:** Change-PC/feat-manual-chapter-upload

Implement a chapter-based manual upload path for authors. Manual projects are a
parallel source type to Scrivener sync and must remain mutually exclusive with
it.

Stage 1 design decisions are recorded in:

- `ADR-ManualChapterUploadArchitecture.md`
- `ManualChapterUploadUXSpec.md`

## Agreed decisions

- Keep the source discriminator on the existing `Project` entity
- Use a dedicated `ManualChapter` entity; do not reuse `Section`
- Store parsed plain text in the database at launch
- Use explicit `SortOrder`
- Support `.txt` and `.docx` only
- Use `DocumentFormat.OpenXml` for `.docx` parsing
- Enforce a 2 MiB per-file limit and a 250 chapter per-project limit

---

## Stage 2 — Domain

### Required behaviour

- Add or amend the project source-type discriminator for
  `ScrivenerSync | ManualUpload`
- Introduce `ManualChapter`
  - `Id`
  - `AuthorId`
  - `ProjectId`
  - `Title`
  - `SortOrder`
  - `RawContent`
  - `OriginalFileName` (nullable — null for paste-origin chapters)
  - `UploadedAt`
- Introduce `ManualChapterVersion` (immutable after creation)
  - `Id`
  - `ManualChapterId`
  - `AuthorId`
  - `VersionNumber`
  - `RawContent`
  - `SnapshotReason` (enum: `FileUpload | Replace | PasteUpload | InlineEdit`)
  - `SnapshotAt`
- Enforce invariants on `ManualChapter`
  - title non-empty
  - sort order >= 0
  - content non-null
- Enforce invariants on `ManualChapterVersion`
  - content non-null
  - version number > 0
- Add `IManualChapterRepository`
- Add `IManualChapterVersionRepository`

### Domain tests required first

- `Create_WithValidData_CreatesManualChapter`
- `Create_WithEmptyTitle_ThrowsInvariantViolation`
- `Create_WithNegativeSortOrder_ThrowsInvariantViolation`
- `Create_WithNullContent_ThrowsInvariantViolation`
- `CreateVersion_WithValidData_CreatesManualChapterVersion`
- `CreateVersion_WithNullContent_ThrowsInvariantViolation`

---

## Stage 3 — Infrastructure

### Required behaviour

- Add EF configuration for `ManualChapter`
- Add EF configuration for `ManualChapterVersion`
- Add migration `AddManualChapters`
- Implement `ManualChapterRepository`
- Implement `ManualChapterVersionRepository`
- Add `IChapterFileParser`
- Implement:
  - `PlainTextChapterParser`
  - `DocxChapterParser`
- Resolve parser by file extension (polymorphic — no switch statements in the
  upload handler)
- Register parsers in DI

### Infrastructure tests required first

- Repository persistence and author scoping for `ManualChapter`
- Repository persistence and author scoping for `ManualChapterVersion`
- Hard-delete path in `ManualChapterVersionRepository.ClearForChapterAsync`
- Parser selection by extension
- `.txt` parsing
- `.docx` parsing via `DocumentFormat.OpenXml`
- Limit enforcement for file type and file size

---

## Stage 4 — Application

### Required behaviour

- Add `ManualUploadService`
  - `UploadChapterAsync` (file-parsed path)
  - `UploadChapterFromPasteAsync` (cut/paste path)
  - `ReorderChaptersAsync`
  - `DeleteChapterAsync`
  - `ReplaceChapterAsync`
  - `EditChapterAsync` (inline edit — updates content, creates version snapshot)
  - `ClearVersionHistoryAsync` (hard delete of all `ManualChapterVersion` rows)
- Reject upload for Scrivener-synced projects
- Emit `AuthorNotification` on successful upload
- On any content change (replace, paste, inline edit), create a
  `ManualChapterVersion` snapshot before overwriting
- Use unit-of-work semantics for multi-step operations

### Application tests required first

- Upload succeeds for manual project
- Upload rejected for Scrivener project
- Paste upload stores content and no filename
- Reorder updates `SortOrder`
- Replace preserves chapter identity and creates a version snapshot
- Inline edit updates content and creates a version snapshot with reason
  `InlineEdit`
- Delete removes only the targeted chapter
- Successful upload writes an `AuthorNotification`
- `ClearVersionHistoryAsync` hard-deletes all versions but leaves current
  chapter content intact

---

## Stage 5 — Web UI

### Required behaviour

- Add upload and reorder actions on the author-facing controller
- Add multipart form with tabbed **Upload file** / **Paste content** input and
  client-side `.txt` / `.docx` validation
- Show a flat chapter list for manual projects
- Provide drag-to-reorder or accessible up/down fallback
- Add **Edit** button per chapter row — expands inline plain-text editor
- Add **History** button per chapter row — shows version list panel with
  **Restore**, **Preview**, and **Clear history** (hard delete) controls
- Hide manual-upload UI for Scrivener-synced projects
- No reader-facing view exposes source type
- Add `DraftView.ManualUpload.css` (explicitly requested by issue #34)
- Bump `--css-version` in `DraftView.Core.css`

### Web verification

- Manual project shows upload controls (file tab and paste tab)
- Scrivener project does not
- Uploaded chapter appears in the list
- Pasted chapter appears in the list with no filename shown
- Replace and reorder behave correctly
- Edit button opens inline editor; Save creates a version snapshot
- History panel lists past versions; Clear history prompts confirmation then
  hard-deletes all version records
- Reader-facing chapter page shows identical HTML regardless of source type

---

## Stage 6 — Verification and polish

- Confirm Domain tests for `ManualChapter` were written first and are green
- Confirm repository integration tests were written first and are green
- Confirm application service tests were written first and are green
- Confirm parser tests for `.txt` and `.docx` were written first and are green
- Smoke test: upload a `.txt` chapter and verify it appears with the parsed
  content

---

## Non-negotiable rules

- One uploaded file represents one chapter
- No automatic chapter splitting
- No mixed manual + Scrivener project mode
- No mapping of manual chapters onto the existing `Section` hierarchy
- No raw file-byte persistence at launch
