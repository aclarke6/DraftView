# RS-C Phase C2 - Comment Creation (Local Execution Phase)

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

Complete **RS-C Phase C2 - Comment Creation** for the **Inline Comments** sprint.

Sprint goal: Create and render comments anchored to selected text.

**Deployable:** NON-DEPLOYABLE
**Reason:** Persisted inline comments require rendering before safe user release.
**Must be deployed with:** C1 and C3

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-C-base` from `main` if it does not already exist.
3. Create `RS-C-base/phase-c2-comment-creation` from `RS-C-base`.
4. All work for this phase must be committed on `RS-C-base/phase-c2-comment-creation`.
5. Developer merges: `RS-C-base/phase-c2-comment-creation` -> `RS-C-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-C / C2 - Comment Creation.

Task index source: `TASKS.md`, RSprint section for **RS-C - Inline Comments**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Extend comment creation to optionally create an anchor.
- Existing non-inline comments remain supported.
- Comment creation uses application service, not controller logic.

---

## Deliverable

- Anchored comment application service behavior.
- Tests for anchored and non-anchored comment creation.

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

- Write failing tests for anchored comment creation.
- Extend comment DTO/service path to accept validated anchor selection.
- Persist comment and anchor atomically through application orchestration.
- Keep current comment creation path working.
---

## Phase-Specific Tests

- Anchored comment persists with PassageAnchor.
- Non-anchored comment still persists with null anchor.
- Invalid anchor selection prevents comment creation.
- Unauthorized reader cannot create anchored comment.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- Controller would need to coordinate repositories directly.
- Anchored comment creation cannot be atomic.
- Existing comments would require mandatory backfill.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- Anchored comments persist.
- Null-anchor comments still work.
- C2 is not deployed alone.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if comments can be created with optional anchors while legacy comments remain valid.
