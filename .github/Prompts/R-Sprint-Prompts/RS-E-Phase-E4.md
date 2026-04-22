# RS-E Phase E4 - Integration (Local Execution Phase)

## Execution Mode

Local Execution Phase.

Apply the **Test Execution Override - Local Phases** rules from `AGENTS.md`.

## Required Reading Order

1. `AGENTS.md`
2. `.github/Instructions/refactoring.instructions.md`
3. `.github/Instructions/versioning.instructions.md`
4. `TASKS.md`
5. `Passage Anchoring, Reader Continuity, and Inline Commentary.md`
6. `PRINCIPLES.md`
7. `REFACTORING.md`

Do not proceed until these documents have been read.

---

## Objective

Complete **RS-E Phase E4 - Integration** for the **Human Override** sprint.

Sprint goal: Allow users to correct automated relocation.

**Deployable:** Yes

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-E-base` from `main` if it does not already exist.
3. Create `RS-E-base/phase-e4-integration` from `RS-E-base`.
4. All work for this phase must be committed on `RS-E-base/phase-e4-integration`.
5. Developer merges: `RS-E-base/phase-e4-integration` -> `RS-E-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-E / E4 - Integration.

Task index source: `TASKS.md`, RSprint section for **RS-E - Human Override**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Add UI/API integration for reject and relink.
- Show status and audit metadata where appropriate.
- Keep controllers thin.

---

## Deliverable

- Reject/relink UI or endpoint integration.
- ViewModels for status/audit metadata.
- Regression tests for authorized and unauthorized operations.

---

## Hard Constraints

### Architecture

- Preserve Domain -> Application -> Infrastructure -> Web layering.
- Keep Web thin: validate input, resolve identity, call Application, map ViewModels, return responses.
- Do not call repositories or DbContext from Web.
- Do not let sync/import create, relocate, or mutate anchors.
- Reader-facing anchor resolution must use `SectionVersion.HtmlContent` when a `SectionVersion` exists.
- Existing comments and read events with null/no anchor data must remain valid.

### Trust and Safety

- Original anchor snapshots are immutable.
- Derived/current matches are replaceable except where user relink has higher authority.
- Orphaned anchors must remain visible where their owning record is visible.
- Confidence must be explicit and must not imply certainty for approximate matches.
- Human relink and rejection outrank automated relocation.

### TDD Rules

- Create stubs with NotImplementedException where the phase requires new production types.
- Write failing tests before production implementation for Domain, Application, and Infrastructure changes.
- Confirm the tests fail for the expected reason before implementation.
- Implement the smallest change that satisfies the tests.
- Run the phase-required tests and any broader suites required by AGENTS.md.
---

## Required Implementation Steps

- Wire Web actions to application service methods.
- Add relink selection UI using the selection capture contract.
- Render rejected/relinked status clearly.
- Add tests for UI/action behavior.
- Run required suites.
---

## Phase-Specific Tests

- Authorized user can reject wrong match.
- Authorized user can relink to correct passage.
- Unauthorized user cannot override.
- Status is visible after override.
- Original context remains accessible.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- UI would hide rejected/orphaned anchors.
- Controller would need to mutate domain entities directly.
- Authorization feedback would leak content existence.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- Users can reject wrong matches and relink to correct passages.
- Status is visible and not misleading.
- E2-E4 can deploy safely.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if human override is usable and accurately represented.
