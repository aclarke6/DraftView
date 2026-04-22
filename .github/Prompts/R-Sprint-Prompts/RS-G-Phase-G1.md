# RS-G Phase G1 - Integration (Cloud Execution Phase)

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

Complete **RS-G Phase G1 - Integration** for the **AI-Assisted Relocation** sprint.

Sprint goal: Use AI only as a last-resort relocation assistant.

**Deployable:** NON-DEPLOYABLE
**Reason:** AI integration needs confidence handling before activation.
**Must be deployed with:** G2 and G3

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-G-base` from `main` if it does not already exist.
3. Create `RS-G-base/phase-g1-integration` from `RS-G-base`.
4. All work for this phase must be committed on `RS-G-base/phase-g1-integration`.
5. Developer merges: `RS-G-base/phase-g1-integration` -> `RS-G-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-G / G1 - Integration.

Task index source: `TASKS.md`, RSprint section for **RS-G - AI-Assisted Relocation**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Integrate with AIScoringService or its established abstraction.
- AI receives bounded candidate/context data, not entire project content.
- AI does not activate user-facing behavior yet.

---

## Deliverable

- AI candidate proposal integration.
- Tests/mocks around bounded input and disabled activation.

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

- Read AIScoringService.md and existing AI abstraction.
- Write failing tests using a fake AI scorer.
- Add adapter/orchestration for AI candidate proposals after deterministic failure.
- Keep feature inactive until G3.
---

## Phase-Specific Tests

- AI receives bounded anchor/context data.
- AI is not called before deterministic matching fails.
- AI proposal does not persist active match before activation.
- AI failures degrade safely.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- AI would need entire project content.
- AI would run before deterministic relocation.
- No fake/test abstraction exists for AI calls.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- AI can propose a candidate without activating user-facing behavior.
- G1 is not deployed alone.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if AI candidate generation is integrated but inactive.
