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
