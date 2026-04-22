# RS-B Phase B4 - Tests (Local Execution Phase)

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

Complete **RS-B Phase B4 - Tests** for the **Anchored Resume** sprint.

Sprint goal: Replace fragile scroll-only resume with anchor-based resume.

**Deployable:** Yes

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-B-base` from `main` if it does not already exist.
3. Create `RS-B-base/phase-b4-tests` from `RS-B-base`.
4. All work for this phase must be committed on `RS-B-base/phase-b4-tests`.
5. Developer merges: `RS-B-base/phase-b4-tests` -> `RS-B-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-B / B4 - Tests.

Task index source: `TASKS.md`, RSprint section for **RS-B - Anchored Resume**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Complete cross-version resume coverage.
- Add regression tests for old behavior fallback.
- Do not add new feature behavior unless needed to make tests pass.

---

## Deliverable

- Domain/Application/Web regression tests for anchored resume.
- Any small fixes required by the tests.

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

- Audit existing B1-B3 coverage.
- Add missing failing tests for exact, context, orphan, legacy scroll fallback, and unauthorized access.
- Fix defects exposed by tests without broad refactor.
- Run required suites.
---

## Phase-Specific Tests

- Exact restore across versions.
- Context/fuzzy restore across versions.
- Orphan fallback.
- Legacy scroll fallback.
- Unauthorized access rejection.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- Testing requires changing documented behavior.
- Missing coverage reveals an architectural gap not covered by the design document.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- Tests cover exact, context, orphan, legacy scroll fallback, and unauthorized access.
- All required tests pass.
- No unrelated behavior changes are included.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if anchored resume behavior is covered by robust regression tests.
