# RS-F Phase F2 - Navigation (Local Execution Phase)

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

Complete **RS-F Phase F2 - Navigation** for the **Original Context** sprint.

Sprint goal: Allow users to inspect the original passage and version.

**Deployable:** NON-DEPLOYABLE
**Reason:** Navigation needs UI integration.
**Must be deployed with:** F3

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-F-base` from `main` if it does not already exist.
3. Create `RS-F-base/phase-f2-navigation` from `RS-F-base`.
4. All work for this phase must be committed on `RS-F-base/phase-f2-navigation`.
5. Developer merges: `RS-F-base/phase-f2-navigation` -> `RS-F-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-F / F2 - Navigation.

Task index source: `TASKS.md`, RSprint section for **RS-F - Original Context**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Provide navigation/highlight data for original passage.
- Map original offsets to rendered content where possible.
- Degrade to context display when mapping fails.

---

## Deliverable

- Navigation/highlight DTOs.
- Tests for mapped and unmapped original context.

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

- Write failing navigation tests.
- Map original canonical offsets into renderable navigation hints.
- Expose fallback context display data.
- Do not add final UI until F3.
---

## Phase-Specific Tests

- Original offsets map to rendered content.
- Mapping failure returns context-only fallback.
- Navigation data does not imply current content.
- Unauthorized access fails.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- Navigation would require mutating stored HTML.
- UI would be unable to distinguish original from current.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- Original offsets can be mapped to rendered content.
- Failures degrade to context display.
- F2 is not deployed alone.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if original context navigation data is available with safe fallback.
