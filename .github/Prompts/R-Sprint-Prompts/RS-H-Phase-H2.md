# RS-H Phase H2 - Author Insight (Local Execution Phase)

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

Complete **RS-H Phase H2 - Author Insight** for the **Reader Insight** sprint.

Sprint goal: Expose anchor-based progress and activity insight.

**Deployable:** NON-DEPLOYABLE
**Reason:** Insight data needs UI integration.
**Must be deployed with:** H3

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-H-base` from `main` if it does not already exist.
3. Create `RS-H-base/phase-h2-author-insight` from `RS-H-base`.
4. All work for this phase must be committed on `RS-H-base/phase-h2-author-insight`.
5. Developer merges: `RS-H-base/phase-h2-author-insight` -> `RS-H-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-H / H2 - Author Insight.

Task index source: `TASKS.md`, RSprint section for **RS-H - Reader Insight**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Application service returns reader activity and progress summaries.
- Respect existing privacy/product decisions.
- Web must not calculate progress.

---

## Deliverable

- Author insight application service/DTOs.
- Authorization and privacy tests.

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

- Write failing author insight tests.
- Build application DTOs for progress summaries.
- Authorize author access only for their project/content.
- Keep UI integration for H3.
---

## Phase-Specific Tests

- Author can query progress summaries for own project.
- Author cannot query other project progress.
- Web receives precomputed DTOs.
- Orphaned progress is represented honestly.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- Privacy rules are unclear.
- Service would expose more reader detail than existing product decisions allow.
- Controller would need aggregation logic.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- Author can query progress summaries through Application layer.
- Web does not calculate progress.
- H2 is not deployed alone.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if author insight data is available through authorized application services.
