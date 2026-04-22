# RS-B Phase B1 - Capture (Cloud Execution Phase)

## Execution Mode

Cloud Execution Phase.

Apply the **Test Execution Override - Cloud Phases** rules from `AGENTS.md`.

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

Complete **RS-B Phase B1 - Capture** for the **Anchored Resume** sprint.

Sprint goal: Replace fragile scroll-only resume with anchor-based resume.

**Deployable:** NON-DEPLOYABLE
**Reason:** Capture without restore creates unused state and inconsistent expectations.
**Must be deployed with:** B2 and B3

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-B-base` from `main` if it does not already exist.
3. Create `RS-B-base/phase-b1-capture` from `RS-B-base`.
4. All work for this phase must be committed on `RS-B-base/phase-b1-capture`.
5. Developer merges: `RS-B-base/phase-b1-capture` -> `RS-B-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-B / B1 - Capture.

Task index source: `TASKS.md`, RSprint section for **RS-B - Anchored Resume**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Add Web capture endpoint or extend existing read-progress endpoint.
- Capture debounced reading position as an anchor purpose.
- Preserve existing read-event fields and active resume behavior.
- Server-side validation must not trust browser offsets as authority.

---

## Deliverable

- Web/Application tests for capture path.
- JavaScript capture with server validation.
- No active replacement of existing resume behavior.

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

- Locate existing read progress/resume paths.
- Write failing tests for capture validation and authorization.
- Add capture DTO and application orchestration.
- Add or update JavaScript to submit debounced position hints.
- Keep old resume path active until B3.
---

## Phase-Specific Tests

- Valid reader position capture creates a resume-purpose anchor.
- Invalid/empty position fails safely.
- Unauthorized user cannot capture progress for inaccessible content.
- Existing read-event update behavior remains intact.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- Capture requires replacing current resume behavior before B3.
- Browser offsets cannot be validated server-side.
- A controller would need to mutate anchors directly.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- Position anchor can be saved.
- Existing resume behavior is still the active behavior until B3.
- B1 is not deployed alone.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if reader position anchors can be captured while existing resume behavior remains active.
