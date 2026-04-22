# AGENT REQUIREMENT - MANDATORY

Before performing any work, the agent MUST:

1. Read and apply:
   - AGENTS.md
   - .github/Instructions/refactoring.instructions.md

2. Operate fully within their constraints

If these files are not read or cannot be applied:

STOP.

Do not proceed with the task.

---
mode: agent
description: S-Sprint-1 Phase 4 — Infrastructure mapping and migration for sync control state
---

# S-Sprint-1 Phase 4 — Infrastructure Mapping and Migration

## Branching
1. Checkout `main` and pull latest from `origin/main`
2. Create `ssprint/S-Sprint-1-Phase-4-infrastructure` from `main`
3. All work on `ssprint/S-Sprint-1-Phase-4-infrastructure`
4. When all Success Gates pass, present merge commands — do not execute
5. Developer merges: `ssprint/S-Sprint-1-Phase-4-infrastructure` → `main`

## Context
S-Sprint-1 Phase 3 is complete. Domain tests prove sync control state derivation rules.

This phase persists the new sync control properties in EF Core and creates the database migration. No user-visible behavior changes yet—the database schema is extended to support webhook sync, but no webhook endpoint or orchestration services exist.

## Reading List
Read the following files in order before writing code:

1. `DraftView.Infrastructure/Data/Configurations/ProjectConfiguration.cs`
   - Read existing `Project` entity configuration
   - Ask: What fluent API patterns are used? Are there existing nullable DateTime columns?

2. `DraftView.Infrastructure/Data/DraftViewDbContext.cs`
   - Read `OnModelCreating` method
   - Ask: How are entity configurations registered?

3. `DropBox Synchronisation Using WebHooks.md`
   - Read section 7.1 (Required control fields)
   - Ask: Which properties must be indexed for query performance?

## Specification

### Task 1: Update `ProjectConfiguration.cs`
Add fluent API configuration for the 9 new sync control properties:

- `SyncRequestedUtc`, `LastWebhookUtc`, `HeldUntilUtc`, `LastSuccessfulSyncUtc`, `LastSyncAttemptUtc`, `SyncLeaseExpiresUtc`: map as nullable `DateTime`
- `SyncLeaseId`: map as nullable `Guid`
- `DropboxCursor`, `LastBackgroundSyncOutcome`: map as nullable `string` with no max length constraint

### Task 2: Add indexes
Background sync queries will filter projects by:
- `SyncRequestedUtc` (find projects with outstanding demand)
- `HeldUntilUtc` (find projects past hold window)
- `SyncLeaseExpiresUtc` (find projects with expired leases)

Add composite index: `(SyncRequestedUtc, HeldUntilUtc, SyncLeaseExpiresUtc)` to support eligibility queries.

### Task 3: Create migration
Migration name: `AddWebhookSyncControlToProjects`

Migration should:
- Add 9 new nullable columns to `Projects` table
- Add composite index
- No data migration required (all fields nullable, default to null)

### Task 4: Verify no behavior change
After migration:
- Existing foreground sync behavior unchanged
- No new services or controllers
- Database schema extended, nothing more

## What to Produce — Plan First, Then Pause

After reading all files, produce a written plan containing all four sections below.

Stop after the plan. Do not write any code. Wait for the plan to be reviewed and approved by the developer.

### Section 1 — Configuration Changes
State precisely:
- What changes to `ProjectConfiguration.cs`
- Which fluent API methods will be used
- Index definition and columns included

### Section 2 — Migration Content
Describe:
- Migration file name
- Columns being added (names, types, nullability)
- Index being created
- Why no `Up` data migration is needed

### Section 3 — Verification Plan
Confirm:
- Migration applies cleanly to empty database
- Migration applies cleanly to existing production-like schema
- No existing tests broken by schema change

### Section 4 — Success Gates

**Gate 1 — Configuration updated**
- [ ] `ProjectConfiguration.cs` modified with fluent API for 9 new properties
- [ ] Composite index added

**Gate 2 — Migration created**
- [ ] Migration file created: `AddWebhookSyncControlToProjects`
- [ ] Migration reviewed — paste `Up` method summary

**Gate 3 — Build succeeds**
- [ ] Solution builds with zero errors

**Gate 4 — Migration applies cleanly**
- [ ] Migration applied to local test database with zero errors

**Gate 5 — No regressions**
- [ ] Full test suite passes — paste count

**Gate 6 — Committed to GitHub**
- [ ] Committed to `ssprint/S-Sprint-1-Phase-4-infrastructure` with message:
    `infra: add EF configuration and migration for Project webhook sync control fields`
- [ ] `git status` is clean

**Gate 7 — TASKS.md updated**
- [ ] S-Sprint-1 Phase 4 marked complete in TASKS.md
- [ ] S-Sprint-1 sprint checkbox marked complete
- [ ] Committed with message: `chore: mark S-Sprint-1 complete in TASKS.md`

**Gate 8 — Present merge commands**
- [ ] Present for manual execution — do not execute:
  ```
  git checkout main
  git merge ssprint/S-Sprint-1-Phase-4-infrastructure
  git push origin main
  ```

## Rules
- Migration must apply cleanly to both empty and existing schemas
- No data migration required (all fields nullable)
- No new services or controllers in this phase
- Follow existing EF Core fluent API patterns
- Migration name must be descriptive: `AddWebhookSyncControlToProjects`
- All git commands are presented to the developer for manual execution
- A task is not complete until every Success Gate is confirmed
