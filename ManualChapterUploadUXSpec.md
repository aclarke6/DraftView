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

The upload modal presents two tabs: **Upload file** and **Paste content**. Both
tabs share the same title field and submit path; only the content origin differs.

### 4a. Upload file tab

- File picker (`.txt` or `.docx` only)
- Title textbox, prefilled from the filename without extension
- Submit button
- Cancel button

### 4b. Paste content tab

- Large resizable textarea labelled "Paste or type your chapter text"
- Title textbox (not prefilled — author must enter it)
- Submit button
- Cancel button
- Pasted content is stored as-is; authors may use markdown syntax (e.g.
  `# Heading`, `**bold**`, `*italic*`) and it will be preserved and rendered
  correctly when the chapter is published

### Validation (both tabs)

- Allowed extensions (file tab): `.txt`, `.docx`
- Max file size: 2 MiB (file tab only)
- Max chapter count per project: 250
- Title required and trimmed
- Content must not be empty

### Success result

- New chapter appears at the end of the flat list
- Success message confirms upload
- Author notification is created in the application layer

## 5. Replace interaction

- Replace keeps the same chapter identity and sort order
- Author selects a new `.txt` or `.docx` file, or pastes replacement text on
  the **Paste content** tab
- Before overwriting, the current content is snapshotted as a
  `ManualChapterVersion` record (see section 8)
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

## 9. Inline chapter editor

Authors may make minor corrections without re-uploading.

### Activation

- An **Edit** button appears on each chapter row
- Clicking **Edit** expands an inline edit zone below the chapter row
- The editor is a plain-text `<textarea>`; markdown syntax entered here is
  respected (same rules as paste upload)

### Edit zone

```
+-------------------------------------------------------------------+
| [↕] 2. Chapter Two            chapter-02.txt  Uploaded 18 Aug    |
|      [Replace file] [Move up] [Move down] [Delete] [Edit] [History]|
|                                                                     |
|  +-----------------------------------------------------------------+|
|  | Chapter Two                                                    ||
|  | +---------------------------------------------------------+    ||
|  | | It was the best of times, it was the worst of...       |    ||
|  | |                                                         |    ||
|  | +---------------------------------------------------resize+    ||
|  |                                           [Save] [Cancel]  |   ||
|  +-----------------------------------------------------------------+|
+-------------------------------------------------------------------+
```

- The textarea is pre-populated with the current `RawContent`
- On **Save**, the content is updated and a `ManualChapterVersion` snapshot is
  created with reason `InlineEdit`
- On **Cancel**, no change is made
- Only one chapter may be in edit mode at a time; opening a second auto-cancels
  the first without saving

## 10. Version history panel

### Access

- A **History** button on each chapter row opens the version history panel
- The panel displays a list of past versions in reverse-chronological order

### Version list row

```
v3 — InlineEdit — 18 Aug 2026 14:02  [Restore] [Preview]
v2 — Replace    — 17 Aug 2026 09:45  [Restore] [Preview]
v1 — FileUpload — 16 Aug 2026 18:00
```

- **Preview** shows the snapshotted `RawContent` in a read-only modal
- **Restore** replaces current content with the version snapshot (creating
  another version snapshot first, with reason `InlineEdit`)
- Version 1 has no restore link (it is the origin)

### Clear history

```
[ Clear all history ]  (requires confirmation dialog)
```

- **Clear history** hard-deletes all `ManualChapterVersion` rows for that chapter
- A confirmation dialog is shown: "This will permanently remove all version
  history for this chapter. The current content will not be affected."
- Hard delete is the only physical-delete path permitted for version records
- After clearing, the History panel shows "No version history."

## 11. Reader transparency

- No reader-facing view exposes source type
- Reader pages render published content from `SectionVersion.HtmlContent`
  regardless of whether the content originated from Scrivener sync or manual
  upload
- "Manual Upload" labelling appears only on author-only screens

## 12. Notes for implementation

- Chapter list labels should use "Chapter" consistently, not "Section"
- Client-side validation is convenience only; server-side validation remains
  authoritative
- `.docx` import maps paragraph styles and character formatting to markdown:
  headings (H1–H3), bold, and italic are preserved; tables, images, and other
  complex elements are stripped
- `.txt` and paste content is stored as-is; markdown syntax written by the
  author is preserved
- Markdown is converted to HTML at publish time; the reader surface is
  unaffected by the content source
- Cut/paste and file-upload tabs share the same server-side handler via a
  command model; the controller does not branch on input origin
