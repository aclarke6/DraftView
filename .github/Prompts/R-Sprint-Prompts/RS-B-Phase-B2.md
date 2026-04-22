# RS-B Phase B2 - Restore (Local Execution Phase)

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

Complete **RS-B Phase B2 - Restore** for the **Anchored Resume** sprint.

Sprint goal: Replace fragile scroll-only resume with anchor-based resume.

**Deployable:** NON-DEPLOYABLE
**Reason:** Restore must be integrated with capture and existing read events.
**Must be deployed with:** B1 and B3

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-B-base` from `main` if it does not already exist.
3. Create `RS-B-base/phase-b2-restore` from `RS-B-base`.
4. All work for this phase must be committed on `RS-B-base/phase-b2-restore`.
5. Developer merges: `RS-B-base/phase-b2-restore` -> `RS-B-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-B / B2 - Restore.

Task index source: `TASKS.md`, RSprint section for **RS-B - Anchored Resume**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Resolve resume anchor when reader opens content.
- Return target offsets or safe fallback to Web.
- Do not remove existing scroll fallback yet.

---

## Deliverable

- Application tests for restored exact/context/orphan behavior.
- Web model fields needed by reader views.

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

- Write failing restore service tests.
- Use anchor resolution service to find current resume target.
- Expose restore target through application DTO/ViewModel mapping.
- Keep legacy fallback path available.
---

## Phase-Specific Tests

- Exact resume anchor restores target location.
- Context/fuzzy resume anchor can restore with confidence metadata.
- Orphaned resume anchor falls back safely.
- Unauthorized reader cannot restore inaccessible content.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- Restore requires reader-facing content from Section.HtmlContent when a SectionVersion exists.
- Fallback behavior would be removed before B3.
- Confidence would be hidden from calling code.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- Resume target is available to views.
- Orphaned resume anchors fall back safely.
- B2 is not deployed alone.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if resume targets can be resolved without replacing the existing reader behavior yet.
