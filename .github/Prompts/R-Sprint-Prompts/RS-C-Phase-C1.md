# RS-C Phase C1 - Selection Capture (Cloud Execution Phase)

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

Complete **RS-C Phase C1 - Selection Capture** for the **Inline Comments** sprint.

Sprint goal: Create and render comments anchored to selected text.

**Deployable:** NON-DEPLOYABLE
**Reason:** Selection capture without comment creation has no user value.
**Must be deployed with:** C2 and C3

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-C-base` from `main` if it does not already exist.
3. Create `RS-C-base/phase-c1-selection-capture` from `RS-C-base`.
4. All work for this phase must be committed on `RS-C-base/phase-c1-selection-capture`.
5. Developer merges: `RS-C-base/phase-c1-selection-capture` -> `RS-C-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-C / C1 - Selection Capture.

Task index source: `TASKS.md`, RSprint section for **RS-C - Inline Comments**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Capture selected text and context from reader content.
- Validate single-section selection.
- Do not persist comments yet.
- Do not corrupt or rewrite story HTML.

---

## Deliverable

- Capture DTO.
- Client capture script.
- Server validation tests.

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

- Identify reader content surfaces.
- Write validation tests for valid and invalid selections.
- Add client capture that sends selected text/context/offset hints.
- Validate selection in application/web boundary without persistence.
---

## Phase-Specific Tests

- Valid single-section selection passes validation.
- Empty selection fails.
- Selection outside reader content fails.
- Selection spanning multiple sections fails.
- Selection across inline markup maps to canonical text.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- Selection mapping cannot be validated server-side.
- Implementation requires storing unvalidated browser offsets as authority.
- Reader content would need unsafe HTML mutation.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- Capture DTO is validated server-side.
- Invalid selections fail safely.
- C1 is not deployed alone.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if selected passages can be captured and validated without creating comments.
