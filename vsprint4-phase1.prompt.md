---
mode: agent
description: V-Sprint 4 Phase 1 — Change Classification Domain
---

# V-Sprint 4 / Phase 1 — Change Classification Domain

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 4.3 and V-Sprint 4
2. Read `REFACTORING.md` in full
3. Read `.github/copilot-instructions.md`
4. Read `DraftView.Domain/Enumerations/ChangeClassification.cs` — enum already exists
5. Read `DraftView.Domain/Entities/SectionVersion.cs` — understand the existing entity
6. Read `DraftView.Domain/Interfaces/Services/IHtmlDiffService.cs` — understand the diff engine
7. Confirm the active branch is `vsprint-4--phase-1-classification-domain`
   — if not on this branch, stop and report
8. Run `git status` — confirm the working tree is clean with no uncommitted changes.
   If uncommitted changes exist that are not part of this phase, stop and report.
9. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Goal

Introduce the classification heuristic at the domain level. Given a diff result
(from `IHtmlDiffService`), classify the nature of changes as `Polish`, `Revision`,
or `Rewrite`. The classification is advisory — it helps the author understand what
they are about to republish.

`ChangeClassification` enum already exists in `DraftView.Domain/Enumerations/`.
`SectionVersion.ChangeClassification` property already exists (nullable) on the entity.

This phase introduces the classification logic as a domain service, and the ability
to set `ChangeClassification` on a `SectionVersion` after creation.

---

## TDD Sequence — Mandatory

1. Create the stub with `throw new NotImplementedException()`
2. Write all failing tests
3. Confirm tests are red
4. Implement to make tests green
5. Run full test suite — zero regressions before committing
6. Commit with `domain:` prefix

---

## Existing Patterns — Follow These Exactly

- Domain enumerations in `DraftView.Domain/Enumerations/`
- Domain service interfaces in `DraftView.Domain/Interfaces/Services/`
- Domain tests in `DraftView.Domain.Tests/`
- Test method naming: `{Method}_{Condition}_{ExpectedOutcome}`
- XML summary on every class and every method
- No external dependencies — pure domain logic

---

## Deliverable 1 — `SectionVersion.SetChangeClassification`

**File:** `DraftView.Domain/Entities/SectionVersion.cs`

Add a domain method to set `ChangeClassification` after creation:

```csharp
/// <summary>
/// Sets the change classification for this version.
/// Called by the application layer after diff-based heuristic classification.
/// Can only be set once — classification is immutable after first assignment.
/// </summary>
/// <param name="classification">The classification to assign.</param>
/// <exception cref="InvariantViolationException">Thrown when classification has already been set.</exception>
public void SetChangeClassification(ChangeClassification classification)
{
    if (ChangeClassification.HasValue)
        throw new InvariantViolationException("I-VER-CLASS",
            "ChangeClassification has already been set and cannot be changed.");

    ChangeClassification = classification;
}
```

### Domain Tests

**File:** `DraftView.Domain.Tests/Entities/SectionVersionTests.cs`

Add to the existing test class:

```
SetChangeClassification_SetsClassification
SetChangeClassification_WhenAlreadySet_ThrowsInvariantViolation
Create_HasNullChangeClassification
```

Run full test suite. Zero regressions.
Commit: `domain: add SetChangeClassification to SectionVersion`

---

## Deliverable 2 — `IChangeClassificationService` Interface

**File:** `DraftView.Domain/Interfaces/Services/IChangeClassificationService.cs`

```csharp
using DraftView.Domain.Diff;
using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Classifies the nature of changes between two versions of prose content.
/// Uses a diff-based heuristic to assign Polish, Revision, or Rewrite.
/// The classification is advisory — it does not block or alter publishing.
/// Source-agnostic: makes no distinction between sync and import content.
/// </summary>
public interface IChangeClassificationService
{
    /// <summary>
    /// Classifies changes based on a paragraph-level diff result.
    /// Returns null when no diff exists (no previous version).
    /// </summary>
    ChangeClassification? Classify(IReadOnlyList<ParagraphDiffResult> diffParagraphs);
}
```

---

## Deliverable 3 — `ChangeClassificationService` Implementation

**File:** `DraftView.Application/Services/ChangeClassificationService.cs`

Implements `IChangeClassificationService`.

### Classification Heuristic

The heuristic operates on the paragraph-level diff results from `IHtmlDiffService`:

1. If `diffParagraphs` is null or empty → return `null` (no diff, no classification)

2. Count total paragraphs, added paragraphs, removed paragraphs, and unchanged paragraphs:
   - `total` = `diffParagraphs.Count`
   - `added` = count where `Type == Added`
   - `removed` = count where `Type == Removed`
   - `changed` = `added + removed`
   - `unchanged` = count where `Type == Unchanged`

3. Calculate change ratio: `changedRatio = (double)changed / total`

4. Apply thresholds:
   - `changedRatio >= 0.6` → `Rewrite` (60%+ of paragraphs changed)
   - `changedRatio >= 0.2` → `Revision` (20–59% of paragraphs changed)
   - `changedRatio > 0` → `Polish` (less than 20% changed)
   - `changedRatio == 0` → return `null` (no changes detected)

These thresholds are named constants — not magic numbers:

```csharp
private const double RewriteThreshold = 0.6;
private const double RevisionThreshold = 0.2;
```

### Tests — `DraftView.Application.Tests/Services/ChangeClassificationServiceTests.cs`

Write all tests **failing** before implementing:

```
Classify_WithNullParagraphs_ReturnsNull
Classify_WithEmptyParagraphs_ReturnsNull
Classify_WithNoChanges_ReturnsNull
Classify_WithMinorChanges_ReturnsPolish
Classify_WithModerateChanges_ReturnsRevision
Classify_WithMajorChanges_ReturnsRewrite
Classify_AtExactRevisionThreshold_ReturnsRevision
Classify_AtExactRewriteThreshold_ReturnsRewrite
Classify_WithOnlyAdditions_ClassifiesCorrectly
Classify_WithOnlyRemovals_ClassifiesCorrectly
```

**Key test expectations:**

- `Classify_WithMinorChanges_ReturnsPolish`: 1 changed out of 10 paragraphs (10%) → `Polish`
- `Classify_WithModerateChanges_ReturnsRevision`: 3 changed out of 10 (30%) → `Revision`
- `Classify_WithMajorChanges_ReturnsRewrite`: 7 changed out of 10 (70%) → `Rewrite`
- `Classify_AtExactRevisionThreshold_ReturnsRevision`: 2 changed out of 10 (20%) → `Revision`
- `Classify_AtExactRewriteThreshold_ReturnsRewrite`: 6 changed out of 10 (60%) → `Rewrite`

Run full test suite. Zero regressions.
Commit: `app: add ChangeClassificationService with diff-based heuristic`

---

## Deliverable 4 — DI Registration

**File:** `DraftView.Web/Extensions/ServiceCollectionExtensions.cs`

In `AddApplicationServices`:

```csharp
services.AddScoped<IChangeClassificationService, ChangeClassificationService>();
```

Run `dotnet build --nologo` to confirm compilation.
Run `dotnet test --nologo` — full suite green.
Commit: `app: register ChangeClassificationService in DI`

---

## Phase Gate — All Must Pass Before Marking Complete

Run `dotnet test --nologo` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline
- [ ] Solution builds without errors
- [ ] `SectionVersion.SetChangeClassification` method exists
- [ ] `IChangeClassificationService` exists in `DraftView.Domain/Interfaces/Services`
- [ ] `ChangeClassificationService` exists in `DraftView.Application/Services`
- [ ] `RewriteThreshold` and `RevisionThreshold` are named constants — no magic numbers
- [ ] `IChangeClassificationService` registered in `ServiceCollectionExtensions.cs`
- [ ] No controller changes
- [ ] No view changes
- [ ] No EF migration required
- [ ] TASKS.md Phase 1 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-4--phase-1-classification-domain`
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

- Calling `IChangeClassificationService` from `VersioningService` — Phase 2
- Persisting `ChangeClassification` on `SectionVersion` via `RepublishChapterAsync` — Phase 2
- Author UI indicator — Phase 3
- Any view or controller changes
