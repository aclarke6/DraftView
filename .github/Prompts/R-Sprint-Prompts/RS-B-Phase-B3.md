# RS-B Phase B3 - Integration (Local Execution Phase)

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

Complete **RS-B Phase B3 - Integration** for the **Anchored Resume** sprint.

Sprint goal: Replace fragile scroll-only resume with anchor-based resume.

**Deployable:** Yes

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-B-base` from `main` if it does not already exist.
3. Create `RS-B-base/phase-b3-integration` from `RS-B-base`.
4. All work for this phase must be committed on `RS-B-base/phase-b3-integration`.
5. Developer merges: `RS-B-base/phase-b3-integration` -> `RS-B-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-B / B3 - Integration.

Task index source: `TASKS.md`, RSprint section for **RS-B - Anchored Resume**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Make anchor-based resume the primary path.
- Keep scroll fallback for legacy/null-anchor records.
- Ensure reader open/read events still update correctly.

---

## Deliverable

- End-to-end reader resume behavior.
- Updated reader view integration.
- Fallback behavior for legacy/null-anchor records.

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

- Wire capture and restore into reader views/controllers through application services.
- Keep controllers thin and ViewModels bound.
- Use anchor restore result first, legacy fallback second.
- Run reader resume regression tests.
---

## Phase-Specific Tests

- Reader resumes from anchor when possible.
- Reader falls back safely when anchor is missing or orphaned.
- Read events still update last-read metadata.
- Existing reader pages render with no anchor data.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- Integration would break existing reader view behavior.
- Web layer would need business logic for resolution.
- Legacy null-anchor behavior cannot be preserved.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- Reader resumes from anchor when possible.
- Reader falls back safely when not possible.
- B1-B3 can deploy together safely.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if anchor-based resume is active with safe fallback.
