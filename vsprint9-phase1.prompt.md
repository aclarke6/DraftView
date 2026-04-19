---
mode: agent
description: V-Sprint 9 Phase 1 — Version Retention Domain
---

# V-Sprint 9 / Phase 1 — Version Retention Domain

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 4.3 and V-Sprint 9
2. Read `REFACTORING.md` in full
3. Read `.github/copilot-instructions.md`
4. Read `DraftView.Domain/Entities/SectionVersion.cs`
5. Read `DraftView.Domain/Interfaces/Repositories/ISectionVersionRepository.cs` — `DeleteAsync` already exists
6. Read `DraftView.Infrastructure/Persistence/Repositories/SectionVersionRepository.cs`
7. Read `DraftView.Domain/Entities/Project.cs` — understand the current Project entity
8. Confirm the active branch is `vsprint-9--phase-1-retention-domain`
   — if not on this branch, stop and report
9. Run `git status` — confirm the working tree is clean with no uncommitted changes.
   If uncommitted changes exist that are not part of this phase, stop and report.
10. Run `.\test-summary.ps1` and record the baseline passing count before touching any code

---

## Goal

Introduce version retention rules at the domain level. Define per-tier limits and
add a domain method to query whether a new version can be created. Phase 1 is
domain and infrastructure only — no enforcement or UI in this phase.

`SectionVersion` physical deletion already exists via `ISectionVersionRepository.DeleteAsync`
(added in V-Sprint 6). This phase does not change that method — it adds the retention
limit domain model that Phase 2 will use to enforce limits.

---

## Design Decisions

**Retention tiers:**

| Tier | Max versions per section |
|------|--------------------------|
| Free | 3 |
| Paid | 10 |
| Ultimate | Unlimited |

These are named constants, not magic numbers. Define them in a
`VersionRetentionPolicy` static class or similar — do not scatter literals.

**Retention check:**
The domain does not enforce retention — it only reports whether the limit is reached.
`VersioningService` (Phase 2) is responsible for acting on that information.

**Unlimited tier:**
`int.MaxValue` as the sentinel for unlimited. Named constant: `VersionRetentionPolicy.Unlimited`.

**No billing entity yet:**
Billing and subscription tiers are deferred to a future sprint. For now, the tier
is represented as a simple `SubscriptionTier` enum, not persisted — it will be
passed in from configuration or hardcoded as `Free` until billing is wired.

---

## TDD Sequence — Mandatory

1. Create stubs with `throw new NotImplementedException()`
2. Write all failing tests
3. Confirm tests are red
4. Implement to make tests green
5. Run full test suite — zero regressions before committing

---

## Deliverable 1 — `SubscriptionTier` Enum

**File:** `DraftView.Domain/Enumerations/SubscriptionTier.cs`

```csharp
namespace DraftView.Domain.Enumerations;

/// <summary>
/// Represents the author's subscription tier.
/// Determines version retention limits per section.
/// Billing integration is deferred — tier is currently fixed at Free.
/// </summary>
public enum SubscriptionTier
{
    Free = 0,
    Paid = 1,
    Ultimate = 2
}
```

---

## Deliverable 2 — `VersionRetentionPolicy` Static Class

**File:** `DraftView.Domain/Policies/VersionRetentionPolicy.cs`

```csharp
namespace DraftView.Domain.Policies;

/// <summary>
/// Defines version retention limits per subscription tier.
/// The only place in the codebase where retention limits are defined.
/// </summary>
public static class VersionRetentionPolicy
{
    /// <summary>Sentinel value for unlimited retention.</summary>
    public const int Unlimited = int.MaxValue;

    /// <summary>Maximum versions per section for Free tier authors.</summary>
    public const int FreeLimit = 3;

    /// <summary>Maximum versions per section for Paid tier authors.</summary>
    public const int PaidLimit = 10;

    /// <summary>
    /// Returns the maximum number of versions permitted per section for the given tier.
    /// </summary>
    public static int GetLimit(SubscriptionTier tier) => tier switch
    {
        SubscriptionTier.Free     => FreeLimit,
        SubscriptionTier.Paid     => PaidLimit,
        SubscriptionTier.Ultimate => Unlimited,
        _                         => FreeLimit
    };

    /// <summary>
    /// Returns true when the existing version count has reached the limit for the given tier.
    /// </summary>
    public static bool IsAtLimit(int existingVersionCount, SubscriptionTier tier)
        => existingVersionCount >= GetLimit(tier);
}
```

---

## Deliverable 3 — `ISectionVersionRepository` Extension

**File:** `DraftView.Domain/Interfaces/Repositories/ISectionVersionRepository.cs`

Add a count query:

```csharp
/// <summary>
/// Returns the number of versions that exist for a given section.
/// Used by the retention enforcement check before creating a new version.
/// </summary>
Task<int> GetVersionCountAsync(Guid sectionId, CancellationToken ct = default);
```

**File:** `DraftView.Infrastructure/Persistence/Repositories/SectionVersionRepository.cs`

Implement:

```csharp
public async Task<int> GetVersionCountAsync(Guid sectionId, CancellationToken ct = default)
    => await _context.SectionVersions
        .CountAsync(v => v.SectionId == sectionId, ct);
```

---

## Deliverable 4 — Tests

**File:** `DraftView.Domain.Tests/Policies/VersionRetentionPolicyTests.cs`

```
GetLimit_Free_ReturnsFreeLimit
GetLimit_Paid_ReturnsPaidLimit
GetLimit_Ultimate_ReturnsUnlimited
IsAtLimit_WhenBelowLimit_ReturnsFalse
IsAtLimit_WhenAtLimit_ReturnsTrue
IsAtLimit_WhenAboveLimit_ReturnsTrue
IsAtLimit_Ultimate_NeverReturnsTrue
```

**File:** `DraftView.Infrastructure.Tests/Repositories/SectionVersionRepositoryTests.cs`
(or equivalent — inspect before writing)

```
GetVersionCountAsync_ReturnsCorrectCount
GetVersionCountAsync_WhenNoVersions_ReturnsZero
```

Run full test suite. Zero regressions.
Commit: `domain: add VersionRetentionPolicy and SubscriptionTier for version retention limits`

---

## Phase Gate — All Must Pass Before Marking Complete

Run `dotnet test -nologo` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline
- [ ] Solution builds without errors
- [ ] `SubscriptionTier` enum exists in `DraftView.Domain/Enumerations`
- [ ] `VersionRetentionPolicy` static class exists in `DraftView.Domain/Policies`
- [ ] `FreeLimit`, `PaidLimit`, `Unlimited` are named constants — no magic numbers
- [ ] `GetLimit` and `IsAtLimit` methods exist and tested
- [ ] `ISectionVersionRepository.GetVersionCountAsync` added and implemented
- [ ] No EF migration required (count query only, no schema change)
- [ ] No controller changes
- [ ] No view changes
- [ ] No enforcement logic in this phase — domain definitions only
- [ ] TASKS.md Phase 1 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-9--phase-1-retention-domain`
- [ ] No warnings in test output linked to phase changes
- [ ] Refactor considered and applied where appropriate, tests green after refactor

---

## Do NOT implement in this phase

- Enforcement in `VersioningService` — Phase 2
- Author prompt to delete old versions — Phase 2
- Version management UI on Publishing Page — Phase 3
- Any view or controller changes
- Any billing or subscription persistence
