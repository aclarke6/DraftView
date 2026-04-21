# BUG-018 — Reader view does not display scene version number

## Summary
Reader view does not display the published version number for a scene, even though version data exists and is correctly surfaced on the Publishing page.

This creates a disconnect between:
- what the author sees (Publishing → v1, v2, etc.)
- what the reader sees (no version information at all)

## Observed Behaviour
- Publishing page shows:
  - Chapter 1 / Scene 2 → `v2`
- Database confirms:
  - SectionVersions exists with VersionNumber = 2 for the scene
- Reader view:
  - Displays scene content
  - Does NOT display any version label

## Expected Behaviour
- Reader view should clearly and persistently display the current version number of the scene being read

## Root Cause Analysis
- Version data is correctly persisted in:
  - `SectionVersions`
- Reader UI does not surface this value:
  - No version field exposed in Reader view models
  - No rendering of version label in DesktopRead or MobileRead views
- This is a **UI/data-shaping gap**, not a persistence or sync issue

## Correct Level of Fix
- Web layer only:
  - expose version number in reader view model
  - render version label in UI
- No changes to:
  - Domain
  - Application logic
  - persistence or version creation

## UX Decision
Version label must be:
- always visible
- tied to the scene being read
- not dependent on banner state

Recommended placement:
- Left-hand scene navigation (preferred — persistent context)
- OR top of reading pane (secondary option)

## Failing Test Plan

### 1. Reader exposes version number
- Class: ReaderControllerTests
- Method: Read_WhenSceneHasVersions_ExposesLatestVersionNumber
- Arrange: scene with versions (v1, v2)
- Act: ReaderController.Read(...)
- Assert: model contains VersionNumber = 2

### 2. Reader does not expose version when none exist
- Method: Read_WhenSceneHasNoVersions_DoesNotExposeVersionNumber
- Arrange: scene with no versions
- Act: Read(...)
- Assert: version is null or absent

### 3. No regression to existing reader behaviour
- Ensure:
  - content renders unchanged
  - navigation unchanged
  - comments unaffected

## Proposed Fix

### Files to change
- ReaderViewModels.cs
  - add VersionNumber to scene model

- MobileReaderViewModels.cs
  - mirror VersionNumber where applicable

- ReaderController.cs
  - map latest version number from SectionVersions

- DesktopRead.cshtml
- MobileRead.cshtml
  - render version label

### Implementation Notes
- Use latest VersionNumber (max)
- Do not compute new logic — reuse existing version data
- Do not introduce ViewBag
- Do not alter reading flow or layout structure beyond minimal addition

## What is NOT changing
- No changes to:
  - versioning logic
  - publishing logic
  - sync logic
  - database schema

## Migration
- None required

## Success Gates

### Gate 1 — Tests red
- [ ] New tests fail before implementation

### Gate 2 — Tests green
- [ ] New tests pass after implementation

### Gate 3 — No regressions
- [ ] Full suite passes (≈768, 0 failed, 1 skipped)

### Gate 4 — Browser verification
- [ ] Open Reader view on a scene with versions
- [ ] Confirm version label is visible (e.g., `v2`)
- [ ] Confirm it remains visible while reading
- [ ] Confirm scenes without versions show no label

### Gate 5 — Git
- [ ] Commit on bugfix/BUG-018-reader-version-display
- Message:
  `bugfix: BUG-018 — show scene version number in reader view`
- [ ] git status clean

### Gate 6 — TASKS.md
- [ ] BUG-018 added and marked [DONE] with resolution summary

### Gate 7 — Merge
- Provide manual merge commands only