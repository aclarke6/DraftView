---
mode: agent
description: V-Sprint 4 Phase 2 — Classification Service Integration
---

# V-Sprint 4 / Phase 2 — Classification Service Integration

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 4.3 and V-Sprint 4
2. Read `REFACTORING.md` in full
3. Read `.github/copilot-instructions.md`
4. Read `DraftView.Application/Services/VersioningService.cs` — understand `RepublishChapterAsync`
5. Read `DraftView.Application/Services/ChangeClassificationService.cs` — understand Phase 1 output
6. Read `DraftView.Domain/Interfaces/Services/IChangeClassificationService.cs`
7. Read `DraftView.Domain/Interfaces/Services/IHtmlDiffService.cs`
8. Read `DraftView.Application.Tests/Services/VersioningServiceTests.cs` — understand existing coverage
9. Confirm the active branch is `vsprint-4--phase-2-classification-service`
   — if not on this branch, stop and report
10. Run `git status` — confirm the working tree is clean with no uncommitted changes.
    If uncommitted changes exist that are not part of this phase, stop and report.
11. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Goal

Wire `IChangeClassificationService` into `VersioningService.RepublishChapterAsync` so
that every `SectionVersion` created during a Republish gets a `ChangeClassification`
assigned based on the diff between the previous version and the new one.

This is an application-layer change only. No controller or view changes in this phase.
The classification is computed and persisted silently — no UI feedback yet.

For first-version sections (no previous `SectionVersion`), classification is skipped —
`SectionVersion.ChangeClassification` remains null.

---

## TDD Sequence — Mandatory

Search existing `VersioningServiceTests.cs` before writing any new tests.
Never write a duplicate test. If an existing test covers the behaviour, verify it
passes rather than rewriting it.

1. Add failing tests to `VersioningServiceTests.cs`
2. Confirm tests are red
3. Modify `VersioningService` to make tests green
4. Run full test suite — zero regressions before committing
5. Commit with `app:` prefix

---

## Existing Patterns — Follow These Exactly

- `VersioningService` already injects `ISectionVersionRepository` and `IHtmlDiffService`
  — check the constructor before adding new dependencies
- Classification must not block republish on failure — wrap in try/catch if needed
- `SectionVersion.SetChangeClassification` is the only permitted path to set classification
- `IHtmlDiffService` is already registered in DI (V-Sprint 2)

---

## Deliverable 1 — Inject `IChangeClassificationService` into `VersioningService`

**File:** `DraftView.Application/Services/VersioningService.cs`

Add `IChangeClassificationService changeClassificationService` to the constructor.

Read the existing constructor before modifying — do not duplicate existing parameters.

---

## Deliverable 2 — Classify During Republish

**File:** `DraftView.Application/Services/VersioningService.cs`

In `RepublishChapterAsync`, after creating each `SectionVersion` and before saving:

1. Load the previous version for this section:
   ```csharp
   var allVersions = await sectionVersionRepo.GetAllBySectionIdAsync(section.Id, ct);
   var previousVersion = allVersions
       .Where(v => v.VersionNumber < newVersion.VersionNumber)
       .OrderByDescending(v => v.VersionNumber)
       .FirstOrDefault();
   ```

2. If a previous version exists, compute the diff and classify:
   ```csharp
   if (previousVersion is not null)
   {
       var diffParagraphs = htmlDiffService.Compute(
           previousVersion.HtmlContent,
           newVersion.HtmlContent);
       var classification = changeClassificationService.Classify(diffParagraphs);
       if (classification.HasValue)
           newVersion.SetChangeClassification(classification.Value);
   }
   ```

3. Classification failure must never block republish — if `SetChangeClassification`
   throws (e.g. already set), log and continue. Do not propagate the exception.

Read the existing method to understand the loop structure before making changes.
The classification logic must fit cleanly inside the per-document iteration.

---

## Deliverable 3 — Tests

**File:** `DraftView.Application.Tests/Services/VersioningServiceTests.cs`

Add to the existing test class. Check existing tests first — do not duplicate.

```
RepublishChapterAsync_SetsChangeClassification_WhenPreviousVersionExists
RepublishChapterAsync_DoesNotSetChangeClassification_WhenNoPreviousVersionExists
RepublishChapterAsync_StillPublishes_WhenClassificationFails
```

**Key test expectations:**

- `RepublishChapterAsync_SetsChangeClassification_WhenPreviousVersionExists`:
  set up a section with an existing `SectionVersion`, mock `ISectionVersionRepository`
  to return it from `GetAllBySectionIdAsync`, mock `IHtmlDiffService` to return
  a diff list, mock `IChangeClassificationService` to return `ChangeClassification.Revision`.
  Verify the new version's `ChangeClassification` is set to `Revision`.

- `RepublishChapterAsync_DoesNotSetChangeClassification_WhenNoPreviousVersionExists`:
  set up a section with no existing versions. Verify `IChangeClassificationService.Classify`
  is never called.

- `RepublishChapterAsync_StillPublishes_WhenClassificationFails`:
  mock `IChangeClassificationService.Classify` to throw. Verify the version is still
  created and saved — classification failure must not block republish.

Run full test suite. Zero regressions.
Commit: `app: classify SectionVersion changes during RepublishChapterAsync`

---

## Phase Gate — All Must Pass Before Marking Complete

Run `dotnet test --nologo` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline
- [ ] Solution builds without errors
- [ ] `VersioningService` injects `IChangeClassificationService`
- [ ] `RepublishChapterAsync` computes diff and classifies when previous version exists
- [ ] Classification is skipped when no previous version exists
- [ ] Classification failure does not block republish
- [ ] `SectionVersion.SetChangeClassification` is the only path used to set classification
- [ ] No controller changes
- [ ] No view changes
- [ ] No EF migration required (field already exists on `SectionVersions` table)
- [ ] TASKS.md Phase 2 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-4--phase-2-classification-service`
- [ ] No warnings in test output linked to phase changes
- [ ] Refactor considered and applied where appropriate, tests green after refactor

---

## Identify All Warnings in Tests

Run `dotnet test --nologo` and identify any warnings in the test output.
Address any warnings that are linked to code changes made in this phase before
proceeding, as they may indicate potential issues in the code.

---

## Refactor Phase

After implementing the above, consider if any refactor is needed to improve code
quality, as per the refactoring guidelines. If so, perform the refactor and ensure
all tests still pass.

---

## Do NOT implement in this phase

- Author UI indicator on Sections view — Phase 3
- Any view or controller changes
- Displaying classification to readers — not in this sprint
- Change classification on manual upload — only on Republish
