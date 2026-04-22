---
mode: agent
description: S-Sprint-1 Phase 3 — Domain tests for control rules
---

# S-Sprint-1 Phase 3 — Domain Tests for Control Rules

## Branching
1. Checkout `main` and pull latest from `origin/main`
2. Create `ssprint/S-Sprint-1-Phase-3-domain-tests` from `main`
3. All work on `ssprint/S-Sprint-1-Phase-3-domain-tests`
4. When all Success Gates pass, present merge commands — do not execute
5. Developer merges: `ssprint/S-Sprint-1-Phase-3-domain-tests` → `main`

## Context
S-Sprint-1 Phase 2 is complete. The `Project` entity now has 9 webhook sync control properties.

This phase adds TDD coverage for the control model. Even though no domain methods exist yet (orchestration services will manipulate properties directly), we need tests proving the control rules work as expected.

## Reading List
Read the following files in order before writing tests:

1. `DropBox Synchronisation Using WebHooks.md`
   - Read section 6 (The Simple Control Model)
   - Read section 7.2 (Derived operational states)
   - Ask: What are the four operational states? How are they derived?

2. `DraftView.Domain/Entities/Project.cs`
   - Read the 9 new sync control properties added in Phase 2
   - Ask: Which combinations of property values represent Idle, Requested, Held, or Syncing states?

3. `DraftView.Domain.Tests/Entities/ProjectTests.cs` (if exists)
   - Read existing test patterns
   - Ask: What test naming convention is used?

## Specification

### Test Coverage Required
Add tests proving the following control rules:

**Lease rules:**
- A project with a valid (non-expired) lease is considered "Syncing"
- A project with an expired lease is available for new lease acquisition
- `SyncLeaseId` and `SyncLeaseExpiresUtc` must both be set or both be null

**Hold rules:**
- A project with `HeldUntilUtc` in the future is "Held"
- A project with `HeldUntilUtc` in the past or null is not held
- Held projects retain `SyncRequestedUtc` (hold delays work, does not discard it)

**Request rules:**
- A project with `SyncRequestedUtc` set has outstanding sync demand
- Outstanding demand persists even when held or leased

**Operational state derivation:**
- Idle: no `SyncRequestedUtc`, no valid lease, not held
- Requested: `SyncRequestedUtc` set, no valid lease, not held
- Held: `SyncRequestedUtc` set, `HeldUntilUtc` in future
- Syncing: valid lease exists (regardless of request or hold state)

### Test Class
Create `ProjectSyncControlTests.cs` in `DraftView.Domain.Tests/Entities/`

### Test Naming Convention
Follow existing pattern: `{Method}_{Condition}_{ExpectedOutcome}`

Examples:
- `SyncControl_WithExpiredLease_IsAvailableForNewLease`
- `SyncControl_WithFutureHeldUntil_IsHeld`
- `SyncControl_WithRequestAndNoLease_IsRequested`

## TDD Sequence

Since no domain methods exist yet, these tests verify property-based state derivation logic that will be used by orchestration services.

**Step 1:** Write helper method `GetOperationalState(Project project)` in test class that derives state from property values

**Step 2:** Write failing tests for each rule above

**Step 3:** Confirm all tests pass (helper method is the "implementation" for now)

## What to Produce — Plan First, Then Pause

After reading all files, produce a written plan containing all four sections below.

Stop after the plan. Do not write any code. Wait for the plan to be reviewed and approved by the developer.

### Section 1 — Test Strategy
State precisely:
- How operational states will be derived from property combinations
- Where the test class will live
- What helper methods are needed in the test class

### Section 2 — Test List
For each test, state:
- Test method name
- What it arranges (property values)
- What it asserts (expected operational state or rule outcome)

### Section 3 — No Domain Method Changes
Confirm:
- No changes to `Project.cs` entity
- Tests use property setters directly
- Helper method lives in test class only

### Section 4 — Success Gates

**Gate 1 — Tests written and red (if applicable)**
- [ ] All tests written
- [ ] Tests use correct naming convention

**Gate 2 — Tests green**
- [ ] All new tests pass — paste passing output

**Gate 3 — No regressions**
- [ ] Full test suite passes — paste count

**Gate 4 — Committed to GitHub**
- [ ] Committed to `ssprint/S-Sprint-1-Phase-3-domain-tests` with message:
    `test: add domain tests for Project sync control state derivation`
- [ ] `git status` is clean

**Gate 5 — TASKS.md updated**
- [ ] S-Sprint-1 Phase 3 marked complete in TASKS.md
- [ ] Committed with message: `chore: mark S-Sprint-1 Phase 3 complete in TASKS.md`

**Gate 6 — Present merge commands**
- [ ] Present for manual execution — do not execute:
  ```
  git checkout main
  git merge ssprint/S-Sprint-1-Phase-3-domain-tests
  git push origin main
  ```

## Rules
- TDD: write tests first, confirm behavior
- Tests prove control rules, not domain methods (none exist yet)
- No changes to `Project.cs` entity
- Follow existing test naming conventions
- All git commands are presented to the developer for manual execution
- A task is not complete until every Success Gate is confirmed
