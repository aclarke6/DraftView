# Manual chapter upload UX spec

**Date:** 2026-08-18  
**Scope:** Issue #32 Stage 1

## 1. UX goals

- Let an author create a project that is explicitly **Manual Upload**
- Make chapter upload feel simple, flat, and predictable
- Never imply that DraftView will split or infer structure inside an uploaded
  file
- Hide all manual-upload controls for Scrivener-synced projects

## 2. Source-type selection in the new-project flow

Add a source-type step to the new-project wizard before any sync-specific setup.

### Options

1. **Scrivener Sync**
   - Description: Connect Dropbox and keep DraftView in sync with the Scrivener
     project structure
2. **Manual Upload**
   - Description: Upload one `.txt` or `.docx` file per chapter and manage a
     flat chapter list in DraftView

### Wizard rule

The source type is mutually exclusive. A project is either Scrivener-synced or
manual-upload; it cannot expose both control sets.

## 3. Manual project screen

Manual projects land on a flat chapter-management screen.

### Empty state

```
+---------------------------------------------------------------+
| Project: The Hollow Road                                      |
| Source: Manual Upload                                         |
+---------------------------------------------------------------+
| Chapters                                                      |
|                                                               |
| No chapters uploaded yet.                                     |
| Upload a .txt or .docx file for your first chapter.           |
|                                                               |
| [ Upload chapter ]                                            |
+---------------------------------------------------------------+
```

### Populated state

```
+-----------------------------------------------------------------------+
| Project: The Hollow Road                              [ Upload chapter ]|
| Source: Manual Upload                                                |
+-----------------------------------------------------------------------+
| Chapters (3 / 250)                                                   |
|                                                                       |
| [↕] 1. Chapter One            chapter-01.docx   Uploaded 18 Aug 2026 |
|      [Replace file] [Move up] [Move down] [Delete]                   |
|                                                                       |
| [↕] 2. Chapter Two            chapter-02.txt    Uploaded 18 Aug 2026 |
|      [Replace file] [Move up] [Move down] [Delete]                   |
|                                                                       |
| [↕] 3. Chapter Three          chapter-03.docx   Uploaded 18 Aug 2026 |
|      [Replace file] [Move up] [Move down] [Delete]                   |
+-----------------------------------------------------------------------+
```

## 4. Upload interaction

### Upload modal / form fields

- File picker
- Title textbox, prefilled from the filename without extension
- Submit button
- Cancel button

### Validation

- Allowed extensions: `.txt`, `.docx`
- Max file size: 2 MiB
- Max chapter count per project: 250
- Title required and trimmed

### Success result

- New chapter appears at the end of the flat list
- Success message confirms upload
- Author notification is created in the application layer

## 5. Replace interaction

- Replace keeps the same chapter identity and sort order
- Author selects a new `.txt` or `.docx` file
- Title may be edited separately; replacement must not silently rename a chapter

## 6. Reordering

- Primary interaction: drag-to-reorder
- Accessible fallback: **Move up** / **Move down** buttons
- Reordering updates explicit `SortOrder`

## 7. Scrivener project behaviour

For `Scrivener Sync` projects:

- Hide manual upload buttons
- Hide chapter replace/delete/reorder controls
- Keep existing sync-oriented project UI

There must be no mixed-state screen where both Scrivener sync controls and
manual upload controls are visible together.

## 8. Atomic-chapter rule

- No UI affordance to split a chapter
- No child rows under a chapter
- No tree builder for manual projects in v1
- Uploaded files are treated as the author's final structural intent

## 9. Notes for implementation

- Chapter list labels should use "Chapter" consistently, not "Section"
- Client-side validation is convenience only; server-side validation remains
  authoritative
- `.docx` import extracts plain text only; rich formatting preview is out of
  scope
