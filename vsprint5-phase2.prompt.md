---
mode: agent
description: V-Sprint 5 Phase 2 — Publish Flow Integration
---

# V-Sprint 5 / Phase 2 — Publish Flow Integration

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 4.3 and V-Sprint 5 Phase 2
2. Read `REFACTORING.md` in full
3. Read `.github/copilot-instructions.md`
4. Read `DraftView.Application/Services/VersioningService.cs` — understand `RepublishChapterAsync`
5. Read `DraftView.Application/Services/AiSummaryService.cs` — understand Phase 1 output
6. Read `DraftView.Web/Controllers/AuthorController.cs` — understand `RepublishChapter`
7. Read `DraftView.Web/Views/Author/Sections.cshtml` — understand the Republish button
8. Read `DraftView.Application.Tests/Services/VersioningServiceTests.cs` — understand existing coverage
9. Confirm the active branch is `vsprint-5--phase-2-publish-flow`
   — if not on this branch, stop and report
10. Run `git status` — confirm the working tree is clean with no uncommitted changes.
    If uncommitted changes exist that are not part of this phase, stop and report.
11. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Goal

Wire `IAiSummaryService` into the Republish flow so that every `SectionVersion`
created during a Republish gets an `AiSummary` generated and persisted.

The summary is generated silently during Republish — there is no confirmation step
or editable textarea in this phase. The author does not see or interact with the
summary during Republish. It is generated, stored, and surfaced to readers in Phase 3.

AI failure must never block Republish. If `IAiSummaryService` returns null or throws,
the version is still created and saved without a summary.

---

## Architecture Decision — No Republish Confirmation Step

The architecture doc mentions showing the summary in an editable textarea on a
Republish confirmation step. This is deferred. In this phase, summary generation
is fully automatic and silent. The confirmation step will be added in a future sprint
when the author workflow is formalised. Do NOT add a confirmation step, redirect to
a confirmation page, or modify the Republish POST flow in any way that adds friction.

---

## TDD Sequence — Mandatory

Search existing `VersioningServiceTests.cs` before writing any new tests.
Never write a duplicate test. If an existing test covers the behaviour, verify it
passes rather than rewriting it.

1. Add failing tests to `VersioningServiceTests.cs`
2. Confirm tests are red
3. Modify `VersioningService` to make tests green
4. Run full test suite — zero regressions before committing

---

## Deliverable 1 — Inject `IAiSummaryService` into `VersioningService`

**File:** `DraftView.Application/Services/VersioningService.cs`

Add `IAiSummaryService aiSummaryService` to the constructor.

Read the existing constructor before modifying — do not duplicate existing parameters.

---

## Deliverable 2 — Generate Summary During Republish

**File:** `DraftView.Application/Services/VersioningService.cs`

In `RepublishChapterAsync`, after creating each `SectionVersion` and after
setting `ChangeClassification` (Phase 2 of V-Sprint 4), generate the AI summary:

1. Load the previous version's `HtmlContent` (already loaded for classification):
   ```csharp
   var previousHtml = previousVersion?.HtmlContent;
   ```

2. Generate the summary:
   ```csharp
   var summary = await aiSummaryService.GenerateSummaryAsync(
       previousHtml,
       section.HtmlContent ?? string.Empty,
       ct);
   ```

3. If a summary was returned, set it on the version:
   ```csharp
   if (summary is not null)
       newVersion.SetAiSummary(summary);
   ```

4. AI failure must never block republish — if `GenerateSummaryAsync` returns null,
   simply continue. Do not add a try/catch here — the service itself guarantees
   null-on-failure.

Read the existing method to understand the loop structure before making changes.
The summary generation must fit inside the per-document iteration, after classification.

---

## Deliverable 3 — Tests

**File:** `DraftView.Application.Tests/Services/VersioningServiceTests.cs`

Add to the existing test class. Check existing tests first — do not duplicate.

```
RepublishChapterAsync_SetsAiSummary_WhenServiceReturnsSummary
RepublishChapterAsync_DoesNotSetAiSummary_WhenServiceReturnsNull
RepublishChapterAsync_StillPublishes_WhenAiSummaryServiceReturnsNull
RepublishChapterAsync_PassesPreviousHtmlToAiService_WhenPreviousVersionExists
RepublishChapterAsync_PassesNullPreviousHtml_WhenNoPreviousVersionExists
```

**Key test expectations:**

- `RepublishChapterAsync_SetsAiSummary_WhenServiceReturnsSummary`:
  mock `IAiSummaryService.GenerateSummaryAsync` to return `"Kira confronts Aldric in the library."`.
  Verify the new version's `AiSummary` equals that string.

- `RepublishChapterAsync_DoesNotSetAiSummary_WhenServiceReturnsNull`:
  mock `IAiSummaryService.GenerateSummaryAsync` to return `null`.
  Verify `newVersion.AiSummary` is null.

- `RepublishChapterAsync_StillPublishes_WhenAiSummaryServiceReturnsNull`:
  mock `IAiSummaryService.GenerateSummaryAsync` to return `null`.
  Verify the version is still created and `SaveChangesAsync` is called.

- `RepublishChapterAsync_PassesPreviousHtmlToAiService_WhenPreviousVersionExists`:
  verify `GenerateSummaryAsync` is called with the previous version's `HtmlContent`
  as the first argument.

- `RepublishChapterAsync_PassesNullPreviousHtml_WhenNoPreviousVersionExists`:
  verify `GenerateSummaryAsync` is called with `null` as the first argument when
  no previous version exists.

Run full test suite. Zero regressions.
Commit: `app: generate AI summary for each SectionVersion during RepublishChapterAsync`

---

## Phase Gate — All Must Pass Before Marking Complete

Run `dotnet test --nologo` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline
- [ ] Solution builds without errors
- [ ] `VersioningService` injects `IAiSummaryService`
- [ ] `RepublishChapterAsync` calls `GenerateSummaryAsync` per document
- [ ] Summary is set via `SetAiSummary` when non-null
- [ ] Summary is skipped when null — no exception
- [ ] Previous version HTML passed correctly — null for first versions
- [ ] No controller changes
- [ ] No view changes
- [ ] No EF migration required (`AiSummary` column already exists)
- [ ] No Republish confirmation step added — silent generation only
- [ ] TASKS.md Phase 2 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-5--phase-2-publish-flow`
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

- Editable summary textarea on Republish — deferred to a future sprint
- Reader banner showing the summary — Phase 3
- Any view or controller changes
- Any changes to the Republish POST action signature
