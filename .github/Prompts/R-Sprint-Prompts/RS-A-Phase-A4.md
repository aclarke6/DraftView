# RS-A Phase A4 - Application Surface (Local Execution Phase)

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

Complete **RS-A Phase A4 - Application Surface** for the **Anchor Foundation** sprint.

Sprint goal: Introduce anchor model, persistence, and application surface without changing UI behavior.

**Deployable:** Yes

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-A-base` from `main` if it does not already exist.
3. Create `RS-A-base/phase-a4-application-surface` from `RS-A-base`.
4. All work for this phase must be committed on `RS-A-base/phase-a4-application-surface`.
5. Developer merges: `RS-A-base/phase-a4-application-surface` -> `RS-A-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-A / A4 - Application Surface.

Task index source: `TASKS.md`, RSprint section for **RS-A - Anchor Foundation**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Add application DTOs and services for anchor create/retrieve.
- Add authorization checks at the application boundary.
- Register service interfaces in DI if required.
- Do not change reader or comment UI yet.

---

## Deliverable

- Application service tests.
- Anchor create/retrieve service surface.
- DTOs for current status and original metadata.

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

- Write failing application tests.
- Add DTOs and service interfaces.
- Implement authorization and orchestration through repositories/unit of work.
- Register services as needed.
- Verify current user behavior is unchanged.
---

## Phase-Specific Tests

- Create anchor for accessible section/version succeeds.
- Create anchor rejects invalid selection.
- Create anchor rejects unauthorized user.
- Retrieve anchor returns status and original metadata.
- Existing non-anchor comment flows are unaffected.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- Authorization cannot be enforced in Application without Web logic.
- Service design requires direct DbContext usage outside Infrastructure.
- Existing comment flows would need behavior changes.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- Anchor service exists but is unused by UI.
- No behavior change for current users.
- All required tests pass.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if the application layer can create and retrieve anchors without UI activation.
