# RS-D Phase D5 - Integration (Local Execution Phase)

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

Complete **RS-D Phase D5 - Integration** for the **Deterministic Relocation** sprint.

Sprint goal: Resolve anchors across versions without AI.

**Deployable:** Yes

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-D-base` from `main` if it does not already exist.
3. Create `RS-D-base/phase-d5-integration` from `RS-D-base`.
4. All work for this phase must be committed on `RS-D-base/phase-d5-integration`.
5. Developer merges: `RS-D-base/phase-d5-integration` -> `RS-D-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-D / D5 - Integration.

Task index source: `TASKS.md`, RSprint section for **RS-D - Deterministic Relocation**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Wire exact/context/fuzzy into application resolution service.
- Resolve anchors for target versions.
- Persist current match or orphan state.
- No AI dependency.

---

## Deliverable

- Integrated deterministic relocation pipeline.
- Application tests for pipeline order and persistence.
- Safe orphan behavior.

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

- Write failing pipeline integration tests.
- Run methods in documented order.
- Persist successful current match or orphan status through application service.
- Ensure manual relinks are not overwritten.
- Run required suites.
---

## Phase-Specific Tests

- Pipeline runs exact before context before fuzzy.
- Successful match persists current match.
- No trustworthy match persists orphan.
- Manual relink is not overwritten.
- No AI service is required.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- Pipeline would overwrite user relink.
- Orphaned anchors would disappear from comments/progress.
- Relocation requires Web-layer business logic.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- Deterministic relocation pipeline is active.
- No AI dependency.
- Orphans degrade safely.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if deterministic relocation is active and safe across versions.
