---
mode: agent
description: S-Sprint-7 Phase 3 - Manual operational controls
---

# S-Sprint-7 Phase 3 - Manual Operational Controls

## Agent Requirements

Before performing any work, read and apply:

1. `AGENTS.md`
2. `.github/Instructions/refactoring.instructions.md`
3. `.github/Instructions/versioning.instructions.md`

This phase touches Dropbox sync and background ingestion. The versioning instructions
therefore apply even though this phase must not create or modify versioning behaviour.

If these files cannot be read or applied, stop and ask for clarification.

## Branching

This task is sprint work, so use a task-specific sub-branch under the sprint parent branch.

1. Confirm the current branch before making changes.
2. Ensure the sprint parent branch is `S-Sprint-7`.
3. Create and work on `S-Sprint-7/phase-3-manual-operational-controls`.
4. Commit only Phase 3 work on that task branch.
5. When all Success Gates pass, present merge commands only. Do not merge to `main`.

If `S-Sprint-7` does not exist locally, stop and ask the developer whether to create it
from `main` or another branch.

## Context

This is Phase 3 of S-Sprint-7, Stale Reconciliation and Operational Hardening.

The architecture brief in `DropBox Synchronisation Using WebHooks.md` says:

> Add minimal operational controls or diagnostics needed for support and troubleshooting,
> without building a large admin UI.

By this point S-Sprint-7 should already have:

- Phase 1 daily stale reconciliation
- Phase 2 structured diagnostics and audit logging

This phase should expose the smallest useful SystemSupport-facing operational surface over
that existing state. It must remain ingestion-only: no publishing, no reader-visible state
changes, and no `SectionVersion` creation.

## Reading List

Read the following files in order before writing code:

1. `DropBox Synchronisation Using WebHooks.md`
   - Read sections 16-19.
   - Read the S-Sprint-7 Goal and Phase 3 brief.
   - Ask: What operational support gaps remain after stale reconciliation and diagnostics?

2. `TASKS.md`
   - Read section 3.1, Dropbox Webhook Sync Sprint Series.
   - Ask: What is the current S-Sprint-7 Phase 3 completion state?

3. `.github/PROMPT-STANDARD.md`
   - Read Sprint Phase Prompt Structure and Anti-Patterns.
   - Ask: What evidence must be captured before declaring this phase done?

4. Existing S-Sprint implementation from prior phases
   - Find the services, repositories, controllers, tests, and views added for S-Sprint-1
     through S-Sprint-7 Phase 2.
   - Ask: What application service already owns sync status, request recording, lease,
     hold, stale reconciliation, and diagnostics?

5. `DraftView.Web/Controllers/SupportController.cs`
   - Read the existing SystemSupport surface.
   - Ask: How can Web remain thin while exposing operational sync diagnostics or controls?

6. `DraftView.Web.Tests/Controllers/SupportControllerTests.cs`
   - Read existing support-controller test style.
   - Ask: What Web-layer authorization and delegation tests are required?

## Specification

### Phase Goal

Add minimal SystemSupport-facing operational diagnostics or controls for background Dropbox
sync support and troubleshooting.

### Required Behaviour

The implemented surface must let SystemSupport understand the current background sync
control state for Scrivener Dropbox projects using data already introduced by the S-Sprint
series, such as:

- last webhook time
- last sync request time
- last sync attempt time
- last successful sync time
- held-until time
- active or expired lease state
- last background sync outcome

If prior phases already provide a safe application service for manual recovery, this phase
may add a minimal control that delegates to that service, such as requesting background sync
for one project.

If no safe application service already exists for a manual control, implement diagnostics
only unless the plan proposes a small application service seam with tests.

### Architectural Rules

- Web must remain a thin HTTP surface.
- Controllers must not call repositories directly.
- Controllers must not mutate domain entities.
- Any operational command must be represented as an Application service method.
- Infrastructure may only implement persistence or integration interfaces.
- Sync remains ingestion-only and must write working state only.
- This phase must not publish content, create `SectionVersion` records, or alter reader
  content resolution.

### Scope Boundaries

Do not build a large admin UI.

Do not add:

- dashboards with charts
- general-purpose job management
- queue infrastructure
- new publishing controls
- reader-facing UI
- project editing workflows
- Dropbox account remapping
- multi-tenancy support

### Expected Shape

Prefer the smallest implementation that fits the existing codebase. A likely shape is:

- Application DTO/service method that returns operational sync rows for support
- Infrastructure repository query only if no suitable query already exists
- SystemSupport-only Web action and view, or an extension of the existing support dashboard
- Focused tests for application behaviour and Web-layer delegation/authorization

If the existing implementation from earlier phases makes a different smaller shape obvious,
use that shape and justify it in the plan.

## TDD Sequence

Follow TDD for Domain, Application, and Infrastructure changes:

1. Identify the smallest missing seam.
2. Add a stub with `throw new NotImplementedException()` where a new Application or
   Infrastructure method is required.
3. Write failing tests first.
4. Confirm the new tests fail for the intended reason and paste the failing output.
5. Implement the minimal behaviour.
6. Confirm the new tests pass and paste the passing output.
7. Run the full test suite and paste the total count.

Web-only display mapping may be tested with existing controller/view test patterns. Do not
place orchestration or business rules in the Web layer.

## What to Produce - Plan First, Then Pause

After reading all files, produce a written plan containing all four sections below.

Stop after the plan. Do not write code. Wait for the plan to be reviewed and approved by
the developer.

### Section 1 - Current State Analysis

State precisely:

- What S-Sprint-7 Phase 1 and Phase 2 code/tests exist
- What operational sync state is already persisted or exposed
- What SupportController/SystemSupport surface currently exists
- Current checkbox state for S-Sprint-7 Phase 3 in `TASKS.md`

### Section 2 - Implementation Plan

Describe in plain English:

- The smallest operational diagnostic/control surface proposed
- Files to create or modify
- Application service methods or DTOs to add or reuse
- Whether any repository query or migration is required
- Tests to write, with arrange/act/assert
- TDD sequence: stub, red, green, refactor

If more than one valid implementation path remains after reading the code, stop and ask the
developer to choose before editing files.

### Section 3 - Verification Steps

Confirm:

- How the operational support surface will be verified
- What browser/manual verification is needed
- How ingestion-only constraints will be checked
- How no-regression testing will be run

### Section 4 - Success Gates

**Gate 1 - Tests written and confirmed red**
- [ ] All new tests written.
- [ ] Tests confirmed red; paste failing output.

**Gate 2 - Implementation complete and tests green**
- [ ] Implementation complete.
- [ ] All new tests pass; paste passing output.

**Gate 3 - No regressions**
- [ ] Full test suite passes; paste count.
- [ ] Confirm no publishing, versioning, or reader-facing behaviour changed.

**Gate 4 - Browser/manual verification**
- [ ] SystemSupport operational diagnostics/control surface manually verified.
- [ ] Authorization verified: non-SystemSupport users cannot access it.

**Gate 5 - Committed to GitHub**
- [ ] Committed to `S-Sprint-7/phase-3-manual-operational-controls` with message:
  `feat: add S-Sprint-7 manual operational controls`
- [ ] `git status` is clean.

**Gate 6 - TASKS.md updated**
- [ ] S-Sprint-7 Phase 3 marked complete in `TASKS.md` using `[DONE]`.
- [ ] Committed with message:
  `chore: mark S-Sprint-7 Phase 3 complete in TASKS.md`

**Gate 7 - Present merge commands**
- [ ] Present for manual execution. Do not execute:

```bash
git checkout S-Sprint-7
git merge S-Sprint-7/phase-3-manual-operational-controls
git checkout main
git merge S-Sprint-7
git push origin main
```

## Rules

- No code before the plan is reviewed and approved.
- TDD: stub, failing test, implementation, confirm green.
- Keep changes minimal and limited to S-Sprint-7 Phase 3.
- Follow Domain -> Application -> Infrastructure -> Web boundaries.
- Web remains thin: validate input, call Application service, map result, return response.
- No repository access from Web.
- No inline styles in Razor views; use existing stylesheet patterns if CSS is needed.
- Webhook sync is ingestion-only; never publish content or create versions.
- Do not add rich admin UI or broad operational tooling.
- All merge commands are presented to the developer for manual execution.
- A task is not complete until every Success Gate is confirmed.
