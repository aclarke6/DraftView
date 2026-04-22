# RS-D Phase D1 - Exact Matching (Cloud Execution Phase)

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

Complete **RS-D Phase D1 - Exact Matching** for the **Deterministic Relocation** sprint.

Sprint goal: Resolve anchors across versions without AI.

**Deployable:** NON-DEPLOYABLE
**Reason:** Exact matching is only one stage of the relocation pipeline.
**Must be deployed with:** D5

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-D-base` from `main` if it does not already exist.
3. Create `RS-D-base/phase-d1-exact-matching` from `RS-D-base`.
4. All work for this phase must be committed on `RS-D-base/phase-d1-exact-matching`.
5. Developer merges: `RS-D-base/phase-d1-exact-matching` -> `RS-D-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-D / D1 - Exact Matching.

Task index source: `TASKS.md`, RSprint section for **RS-D - Deterministic Relocation**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Implement exact normalized-text matching.
- No UI activation.
- Do not use AI.
- Do not silently resolve duplicate exact matches.

---

## Deliverable

- Exact matching component/service.
- Tests for unique, none, and duplicate exact matches.

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

- Write failing exact matching tests.
- Use canonical text from the established canonicalization contract.
- Implement unique exact match behavior with confidence 100.
- Pass duplicate/no match cases to unresolved result for later stages.
---

## Phase-Specific Tests

- Unique exact match returns confidence 100.
- No exact match returns no match.
- Duplicate exact matches do not choose silently.
- Whitespace/entity normalization is respected.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- Canonical text generation is not available or not deterministic.
- Duplicate behavior cannot be represented without changing the result model.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- Exact unique matches return confidence 100.
- Duplicate exact matches do not silently choose the wrong location.
- D1 is not deployed alone.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if exact matching is deterministic and safe for pipeline integration.
