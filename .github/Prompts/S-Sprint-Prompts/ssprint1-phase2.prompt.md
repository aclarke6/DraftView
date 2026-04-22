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
description: S-Sprint-1 Phase 2 — Domain model for sync control state
---

# S-Sprint-1 Phase 2 — Domain Model for Sync Control

## Branching
1. Checkout `main` and pull latest from `origin/main`
2. Create `S-Sprint-1-base` from `main` if it does not already exist
3. Create `S-Sprint-1-base/phase-2-domain-model` from `S-Sprint-1-base`
4. All work on `S-Sprint-1-base/phase-2-domain-model`
5. Developer merges: `S-Sprint-1-base/phase-2-domain-model` -> `S-Sprint-1-base` -> `main`

## Context
S-Sprint-1 Phase 1 is complete. TASKS.md has been updated to reflect the start of the webhook sync series.

This phase adds the domain-level sync control properties needed to support background webhook-driven sync. These properties enable request tracking, cooldown (hold), lease management, and cursor storage on the existing `Project` entity.

**Key constraint:** This is single-author mode. No `DropboxAccountId` field is added yet. Multi-tenancy support is deferred to MT-Sprint-1.

## Reading List
Read the following files in order before writing code:

1. `DropBox Synchronisation Using WebHooks.md`
   - Read section 7.1 (Required control fields)
   - Ask: What properties are needed per project for webhook sync control?

2. `DraftView.Domain/Entities/Project.cs`
   - Read existing properties
   - Ask: Which Dropbox-specific fields already exist?

3. `Publishing And Versioning Architecture.md`
   - Read section 4.1 (Project entity)
   - Ask: What is `ProjectType` and why does it matter?

## Specification

### Domain Changes
Add the following nullable DateTime and Guid properties to the `Project` entity:

| Property | Type | Description |
|----------|------|-------------|
| `SyncRequestedUtc` | `DateTime?` | Timestamp of most recent durable request for sync |
| `LastWebhookUtc` | `DateTime?` | Most recent webhook touch for audit |
| `HeldUntilUtc` | `DateTime?` | Cooldown boundary — if in future, immediate sync must not proceed |
| `LastSuccessfulSyncUtc` | `DateTime?` | Timestamp of most recent successful sync (foreground or background) |
| `LastSyncAttemptUtc` | `DateTime?` | Timestamp of most recent attempted sync |
| `SyncLeaseId` | `Guid?` | Unique lease token for active sync processor |
| `SyncLeaseExpiresUtc` | `DateTime?` | Lease expiry to protect against abandoned workers |
| `DropboxCursor` | `string?` | Saved Dropbox cursor for change interrogation |
| `LastBackgroundSyncOutcome` | `string?` | Optional status summary for diagnostics |

### Invariants
- All new properties are nullable (no migration data population required)
- No factory method changes — these are operational fields, not creation concerns
- No domain method changes yet — orchestration services will manipulate these fields directly

### What NOT to Add
- Do **not** add `DropboxAccountId` — deferred to MT-Sprint-1
- Do **not** add a `SyncState` enum — states are derived from field values
- Do **not** add domain methods for lease acquisition or hold evaluation — those belong in application services (Phase 3)

## What to Produce — Plan First, Then Pause

After reading all files, produce a written plan containing all four sections below.

Stop after the plan. Do not write any code. Wait for the plan to be reviewed and approved by the developer.

### Section 1 — Current State Analysis
State precisely:
- Current properties on `Project` entity
- Whether `ProjectType` enum exists and what values it has
- Whether any sync-related properties already exist (e.g., `LastSyncedAt`, `SyncStatus`)

### Section 2 — Proposed Domain Changes
Describe in plain English:
- The 9 new properties being added to `Project`
- Why each is nullable
- Why no domain methods are needed yet

### Section 3 — No-Change Verification
Confirm:
- No factory method changes
- No existing properties removed or renamed
- No domain tests needed yet (control rule tests come in Phase 3)

### Section 4 — Success Gates

**Gate 1 — Domain changes applied**
- [ ] All 9 properties added to `Project.cs`
- [ ] All properties are nullable
- [ ] No factory method changes

**Gate 2 — Build succeeds**
- [ ] Solution builds with zero errors

**Gate 3 — No regressions**
- [ ] Full test suite passes — paste count

**Gate 4 — Committed to GitHub**
- [ ] Committed to `S-Sprint-1-base/phase-2-domain-model` with message:
    `domain: add webhook sync control properties to Project entity`
- [ ] `git status` is clean

**Gate 5 — TASKS.md updated**
- [ ] S-Sprint-1 Phase 2 marked complete in TASKS.md
- [ ] Committed with message: `chore: mark S-Sprint-1 Phase 2 complete in TASKS.md`

**Gate 6 — Present merge commands**
- [ ] Present for manual execution — do not execute:
  ```
  git checkout S-Sprint-1-base
  git merge S-Sprint-1-base/phase-2-domain-model
  git checkout main
  git merge S-Sprint-1-base
  git push origin main
  ```

## Rules
- No code before the plan is reviewed
- No domain methods yet — properties only
- No `DropboxAccountId` field — single-author mode
- All git commands are presented to the developer for manual execution
- A task is not complete until every Success Gate is confirmed
