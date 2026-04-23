# RS-C Phase C3 - Rendering (Local Execution Phase)

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

Complete **RS-C Phase C3 - Rendering** for the **Inline Comments** sprint.

Sprint goal: Create and render comments anchored to selected text.

**Deployable:** Yes, when combined with C1 and C2

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-C-base` from `main` if it does not already exist.
3. Create `RS-C-base/phase-c3-rendering` from `RS-C-base`.
4. All work for this phase must be committed on `RS-C-base/phase-c3-rendering`.
5. Developer merges: `RS-C-base/phase-c3-rendering` -> `RS-C-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-C / C3 - Rendering.

Task index source: `TASKS.md`, RSprint section for **RS-C - Inline Comments**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Render inline indicators/highlights for anchored comments.
- Keep comment list/sidebar available.
- Mark approximate/orphaned anchors clearly.

---

## Deliverable

- Reader UI rendering for anchored comments.
- Commented text should be highlighted in the story content.
- Hovering over this highlighted text should show a Tooltip with comment preview (author, date, truncated content).
- Clicking on the highlighted comment should open a modal box with comment details, including responses.
- Modal box should allow deleting, editing, and responding to comments.
- Author features for comments should be preserved in the modal box.
- ViewModels and CSS/JS needed for indicators.
- Regression tests for rendering states.

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

- Add application DTO data needed by Web.
- Map anchor status into ViewModels.
- Render indicators without modifying stored story HTML.
- Show orphaned/approximate/relinked/rejected status clearly.
---

## Phase-Specific Tests

- Inline comment indicator renders for located anchor.
- Orphaned comment remains visible.
- Legacy null-anchor comment renders normally.
- Approximate status is visible where surfaced.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- Rendering would require mutating SectionVersion.HtmlContent.
- Orphaned comments would disappear.
- ViewModel includes unused properties.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- Inline comments are visible without corrupting story content.
- Orphaned comments remain visible.
- C1-C3 can deploy together safely.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if anchored comments render inline with safe fallback states.
