# RS-A Phase A1 - Model Discovery (Cloud Execution Phase)

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

Complete **RS-A Phase A1 - Model Discovery** for the **Anchor Foundation** sprint.

Sprint goal: Introduce anchor model, persistence, and application surface without changing UI behavior.

**Deployable:** Yes

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-A/base` from `main` if it does not already exist.
3. Create `RS-A/phase-a1-model-discovery` from `RS-A/base`.
4. All work for this phase must be committed on `RS-A/phase-a1-model-discovery`.
5. Developer merges: `RS-A/phase-a1-model-discovery` -> `RS-A/base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-A / A1 - Model Discovery.

Task index source: `TASKS.md`, RSprint section for **RS-A - Anchor Foundation**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Inspect Comment, ReadEvent, Section, SectionVersion, reader views, author views, repositories, and current services.
- Propose final class names, relationships, repository shape, and migration strategy.
- Identify whether ReadEvent is sufficient for anchor-based progress or whether a separate progress record is needed later.
- Do not change production code, tests, migrations, or runtime behavior.

---

## Deliverable

- A written proposal only.
- No production code.
- No migrations.
- Completed A1 decision must be recorded in `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 3.1.1.

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

- No tests are required because this is discovery-only.
- Do not run or modify the test suite unless needed to inspect current behavior.
---

## Required Implementation Steps

- Read the required documents and inspect the existing model.
- Map existing comment, read event, section, and version relationships.
- Propose RS-A A2-A4 files/classes/services/repositories/migrations.
- Confirm whether the A1-selected nullable links are `Comment.PassageAnchorId` and `ReadEvent.ResumeAnchorId`.
- Call out any deviations needed from the architecture document before implementation phases begin.
---

## Phase-Specific Tests

- No tests are required because this is discovery-only.
- Do not run or modify the test suite unless needed to inspect current behavior.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- Current model cannot support additive migration.
- Proposed design would require reader content to use Section.HtmlContent when a SectionVersion exists.
- Branching or ownership rules conflict with the repository instructions.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- Proposal names exact files/classes to change in A2-A4.
- Proposal confirms the selected branch pattern is `RS-A/base` and `RS-A/phase-*`.
- Proposal confirms how legacy comments/read events remain valid.
- Proposal confirms versioning boundary compliance.
- No code or migration changes are made.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if it produces a clear A2-A4 implementation proposal without changing code.
