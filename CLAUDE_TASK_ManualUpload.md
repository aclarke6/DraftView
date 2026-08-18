# Task: Manual chapter upload — text and Word documents
**Date:** 2026-08-18
**Branch:** copilot/feat-manual-chapter-upload

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
  - `OriginalFileName`
  - `UploadedAt`
- Enforce invariants
  - title non-empty
  - sort order >= 0
  - content non-null
- Add `IManualChapterRepository`

### Domain tests required first

- `Create_WithValidData_CreatesManualChapter`
- `Create_WithEmptyTitle_ThrowsInvariantViolation`
- `Create_WithNegativeSortOrder_ThrowsInvariantViolation`
- `Create_WithNullContent_ThrowsInvariantViolation`

---

## Stage 3 — Infrastructure

### Required behaviour

- Add EF configuration for `ManualChapter`
- Add migration `AddManualChapters`
- Implement `ManualChapterRepository`
- Add `IChapterFileParser`
- Implement:
  - `PlainTextChapterParser`
  - `DocxChapterParser`
- Resolve parser by file extension
- Register parsers in DI

### Infrastructure tests required first

- Repository persistence and author scoping
- Parser selection by extension
- `.txt` parsing
- `.docx` parsing via `DocumentFormat.OpenXml`
- Limit enforcement for file type and file size

---

## Stage 4 — Application

### Required behaviour

- Add `ManualUploadService`
  - `UploadChapterAsync`
  - `ReorderChaptersAsync`
  - `DeleteChapterAsync`
  - `ReplaceChapterAsync`
- Reject upload for Scrivener-synced projects
- Emit `AuthorNotification` on successful upload
- Use unit-of-work semantics for multi-step operations

### Application tests required first

- Upload succeeds for manual project
- Upload rejected for Scrivener project
- Reorder updates `SortOrder`
- Replace preserves chapter identity
- Delete removes only the targeted chapter
- Successful upload writes an `AuthorNotification`

---

## Stage 5 — Web UI

### Required behaviour

- Add upload and reorder actions on the author-facing controller
- Add multipart form with client-side `.txt` / `.docx` validation
- Show a flat chapter list for manual projects
- Provide drag-to-reorder or accessible up/down fallback
- Hide manual-upload UI for Scrivener-synced projects
- Add `DraftView.ManualUpload.css`
- Bump `--css-version` in `DraftView.Core.css`

### Web verification

- Manual project shows upload controls
- Scrivener project does not
- Uploaded chapter appears in the list
- Replace and reorder behave correctly

---

## Stage 6 — Tests and polish

- Domain tests for `ManualChapter`
- Repository integration tests
- Application service tests
- Parser tests for `.txt` and `.docx`
- Smoke test: upload a `.txt` chapter and verify it appears with the parsed
  content

---

## Non-negotiable rules

- One uploaded file represents one chapter
- No automatic chapter splitting
- No mixed manual + Scrivener project mode
- No mapping of manual chapters onto the existing `Section` hierarchy
- No raw file-byte persistence at launch
