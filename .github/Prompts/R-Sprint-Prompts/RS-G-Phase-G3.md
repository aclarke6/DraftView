# RS-G Phase G3 - Activation (Local Execution Phase)

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

Complete **RS-G Phase G3 - Activation** for the **AI-Assisted Relocation** sprint.

Sprint goal: Use AI only as a last-resort relocation assistant.

**Deployable:** Yes

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-G-base` from `main` if it does not already exist.
3. Create `RS-G-base/phase-g3-activation` from `RS-G-base`.
4. All work for this phase must be committed on `RS-G-base/phase-g3-activation`.
5. Developer merges: `RS-G-base/phase-g3-activation` -> `RS-G-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-G / G3 - Activation.

Task index source: `TASKS.md`, RSprint section for **RS-G - AI-Assisted Relocation**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Enable AI recovery after deterministic pipeline fails.
- Use feature flag or configuration gate if needed.
- Keep user relink and rejection authoritative.

---

## Deliverable

- Activated AI fallback path.
- Configuration/feature flag if needed.
- Regression tests for last-resort behavior.

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

- Wire AI fallback after deterministic pipeline failure.
- Apply confidence threshold and cap.
- Persist AI match only when accepted by policy.
- Ensure failures become orphan.
- Run required suites.
---

## Phase-Specific Tests

- AI is called only after deterministic failure.
- Accepted AI match persists with AiMatched status.
- Low-confidence AI result becomes orphan.
- Manual relink/rejection remains authoritative.
- AI service failure degrades safely.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- Activation would make AI mandatory for deterministic relocation.
- AI failures would break reader/comment rendering.
- Configuration for AI availability is unclear.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- AI is last resort.
- User relink and rejection remain authoritative.
- Failures degrade to orphan.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if AI recovery is active only as a bounded last-resort fallback.
