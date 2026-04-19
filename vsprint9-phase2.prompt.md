---
mode: agent
description: V-Sprint 9 Phase 2 — Retention Enforcement
---

# V-Sprint 9 / Phase 2 — Retention Enforcement

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` V-Sprint 9 Phase 2
2. Read `REFACTORING.md` in full
3. Read `.github/copilot-instructions.md`
4. Read `DraftView.Domain/Policies/VersionRetentionPolicy.cs` — Phase 1 output
5. Read `DraftView.Domain/Enumerations/SubscriptionTier.cs` — Phase 1 output
6. Read `DraftView.Application/Services/VersioningService.cs` — understand `RepublishChapterAsync` and `RepublishSectionAsync`
7. Read `DraftView.Application.Tests/Services/VersioningServiceTests.cs` — understand existing coverage
8. Confirm the active branch is `vsprint-9--phase-2-retention-enforcement`
   — if not on this branch, stop and report
9. Run `git status` — confirm the working tree is clean with no uncommitted changes.
   If uncommitted changes exist that are not part of this phase, stop and report.
10. Run `.\test-summary.ps1` and record the baseline passing count before touching any code

---

## Goal

Enforce version retention limits in `VersioningService` before creating new versions.
When a section has reached its tier limit, the author must delete an old version
before a new one can be created.

This phase adds the enforcement check and the appropriate exception. It does not
add UI for managing or deleting versions — that is Phase 3.

---

## Design Decisions

**Tier source:**
Until billing is wired, the tier is sourced from `IConfiguration` under
`"DraftView:SubscriptionTier"` (default: `Free` if missing or unparseable).
Inject `IConfiguration` into `VersioningService` if not already present.
Read the existing constructor before modifying.

**Exception:**
Introduce `VersionRetentionLimitException` in the domain:

```csharp
public class VersionRetentionLimitException : DomainException
{
    public VersionRetentionLimitException(int limit)
        : base($"Version limit of {limit} reached. Delete an older version before publishing again.") { }
}
```

Place in `DraftView.Domain/Exceptions/`.

**Enforcement point:**
The check happens in `VersioningService` before `SectionVersion.Create()` is called,
for both `RepublishChapterAsync` (per document) and `RepublishSectionAsync`.

---

## TDD Sequence — Mandatory

Search existing `VersioningServiceTests.cs` before writing. Never duplicate a test.

1. Add `VersionRetentionLimitException` stub
2. Write all failing tests
3. Implement enforcement in `VersioningService`
4. Run full test suite — zero regressions before committing

---

## Deliverable 1 — `VersionRetentionLimitException`

**File:** `DraftView.Domain/Exceptions/VersionRetentionLimitException.cs`

```csharp
namespace DraftView.Domain.Exceptions;

/// <summary>
/// Thrown when a new version cannot be created because the section has
/// reached the retention limit for the author's subscription tier.
/// The author must delete an older version before publishing again.
/// </summary>
public class VersionRetentionLimitException : DomainException
{
    public int Limit { get; }

    public VersionRetentionLimitException(int limit)
        : base($"Version limit of {limit} reached. Delete an older version before publishing again.")
    {
        Limit = limit;
    }
}
```

Check what `DomainException` base class is used in the project — read existing
exceptions before implementing. Use the same base class pattern.

---

## Deliverable 2 — Enforcement in `VersioningService`

**File:** `DraftView.Application/Services/VersioningService.cs`

In both `RepublishChapterAsync` and `RepublishSectionAsync`, before
`SectionVersion.Create()` for each document:

```csharp
var tier = GetSubscriptionTier();
var versionCount = await sectionVersionRepository.GetVersionCountAsync(document.Id, ct);
if (VersionRetentionPolicy.IsAtLimit(versionCount, tier))
    throw new VersionRetentionLimitException(VersionRetentionPolicy.GetLimit(tier));
```

Add a private helper to resolve the tier from configuration:

```csharp
private SubscriptionTier GetSubscriptionTier()
{
    var raw = configuration["DraftView:SubscriptionTier"];
    return Enum.TryParse<SubscriptionTier>(raw, ignoreCase: true, out var tier)
        ? tier
        : SubscriptionTier.Free;
}
```

Read the existing constructor before modifying — do not duplicate injected dependencies.

---

## Deliverable 3 — Tests

**File:** `DraftView.Application.Tests/Services/VersioningServiceTests.cs`

Add to the existing test class. Check for duplicates first.

```
RepublishChapterAsync_WhenAtRetentionLimit_ThrowsVersionRetentionLimitException
RepublishChapterAsync_WhenBelowRetentionLimit_CreatesVersion
RepublishSectionAsync_WhenAtRetentionLimit_ThrowsVersionRetentionLimitException
RepublishSectionAsync_WhenBelowRetentionLimit_CreatesVersion
RepublishChapterAsync_WhenTierIsUltimate_NeverThrowsRetentionException
```

**Test setup:**
- Mock `ISectionVersionRepository.GetVersionCountAsync` to return a count at or above the limit
- Configure `IConfiguration["DraftView:SubscriptionTier"]` to return `"Free"` for limit tests
- Configure to return `"Ultimate"` for the unlimited test

Run full test suite. Zero regressions.
Commit: `app: enforce version retention limits in VersioningService`

---

## Deliverable 4 — Controller Error Handling

**File:** `DraftView.Web/Controllers/AuthorController.cs`

In `RepublishChapter` and `RepublishDocument` actions, the existing try/catch
already catches `Exception` and sets `TempData["Error"]`. `VersionRetentionLimitException`
will be caught there automatically.

Verify the existing error handling is sufficient — read both actions before deciding
if any change is needed. If the message is already surfaced correctly via `TempData["Error"]`,
no change is required.

---

## Phase Gate — All Must Pass Before Marking Complete

Run `.\test-summary.ps1` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline
- [ ] Solution builds without errors
- [ ] `VersionRetentionLimitException` exists in `DraftView.Domain/Exceptions`
- [ ] Enforcement added to `RepublishChapterAsync` per document
- [ ] Enforcement added to `RepublishSectionAsync`
- [ ] `GetSubscriptionTier()` reads from `IConfiguration["DraftView:SubscriptionTier"]`
- [ ] Default tier is `Free` when config is missing or unparseable
- [ ] Ultimate tier never throws retention exception
- [ ] Controller error handling verified — `TempData["Error"]` surfaces the message
- [ ] No EF migration required
- [ ] No view changes required
- [ ] TASKS.md Phase 2 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-9--phase-2-retention-enforcement`
- [ ] No warnings in test output linked to phase changes
- [ ] Refactor considered and applied where appropriate, tests green after refactor

---

## Do NOT implement in this phase

- Version management UI — Phase 3
- Delete version action — Phase 3
- Billing or subscription persistence — future sprint
- Any changes to the reader experience
