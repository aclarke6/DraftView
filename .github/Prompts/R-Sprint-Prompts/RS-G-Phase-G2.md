# RS-G Phase G2 - Confidence Handling (Local Execution Phase)

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

Complete **RS-G Phase G2 - Confidence Handling** for the **AI-Assisted Relocation** sprint.

Sprint goal: Use AI only as a last-resort relocation assistant.

**Deployable:** NON-DEPLOYABLE
**Reason:** Confidence handling must be activated with the AI pipeline.
**Must be deployed with:** G3

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-G-base` from `main` if it does not already exist.
3. Create `RS-G-base/phase-g2-confidence-handling` from `RS-G-base`.
4. All work for this phase must be committed on `RS-G-base/phase-g2-confidence-handling`.
5. Developer merges: `RS-G-base/phase-g2-confidence-handling` -> `RS-G-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-G / G2 - Confidence Handling.

Task index source: `TASKS.md`, RSprint section for **RS-G - AI-Assisted Relocation**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Cap AI confidence.
- Combine AI rationale with deterministic evidence where available.
- AI matches below threshold become orphaned.

---

## Deliverable

- AI confidence policy.
- Tests for threshold, cap, and orphan behavior.

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

- Write failing AI confidence tests.
- Implement confidence cap and activation threshold.
- Ensure AI cannot claim exact confidence.
- Expose rationale where appropriate without overclaiming certainty.
---

## Phase-Specific Tests

- AI matches below threshold become orphaned.
- AI confidence is capped.
- AI cannot claim exact confidence.
- User relink/rejection outranks AI.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- AI scorer does not return enough data to explain confidence.
- AI output would override manual relink or rejection.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- AI matches below threshold become orphaned.
- AI cannot claim exact confidence.
- G2 is not deployed alone.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if AI confidence is bounded and cannot mislead users.
