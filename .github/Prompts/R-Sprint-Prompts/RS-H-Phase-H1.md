# RS-H Phase H1 - Progress Tracking (Cloud Execution Phase)

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

Complete **RS-H Phase H1 - Progress Tracking** for the **Reader Insight** sprint.

Sprint goal: Expose anchor-based progress and activity insight.

**Deployable:** NON-DEPLOYABLE
**Reason:** Tracking data needs author insight/UI to be useful.
**Must be deployed with:** H2 and H3

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-H-base` from `main` if it does not already exist.
3. Create `RS-H-base/phase-h1-progress-tracking` from `RS-H-base`.
4. All work for this phase must be committed on `RS-H-base/phase-h1-progress-tracking`.
5. Developer merges: `RS-H-base/phase-h1-progress-tracking` -> `RS-H-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-H / H1 - Progress Tracking.

Task index source: `TASKS.md`, RSprint section for **RS-H - Reader Insight**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Persist anchor-based progress in or alongside ReadEvent.
- Do not expose new author UI yet.
- Keep existing read event behavior working.

---

## Deliverable

- Progress tracking application/domain behavior.
- Tests for latest reader position and legacy read events.

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

- Use A1 decision about ReadEvent versus separate progress record.
- Write failing progress tracking tests.
- Persist latest known reader position as anchor-based progress.
- Keep current read-event behavior intact.
---

## Phase-Specific Tests

- Progress anchor records latest known reader position.
- Existing read event behavior still works.
- Unauthorized progress write fails.
- Orphaned progress anchor does not delete read history.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- Progress tracking would expose reader-private data before H2/H3 decisions.
- ReadEvent changes would break existing version/update messaging.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- Progress anchor records latest known reader position.
- Existing read event behavior still works.
- H1 is not deployed alone.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if reader progress can be tracked with anchors without changing author UI yet.
