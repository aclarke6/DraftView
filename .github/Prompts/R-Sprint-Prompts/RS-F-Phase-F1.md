# RS-F Phase F1 - Retrieval (Cloud Execution Phase)

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

Complete **RS-F Phase F1 - Retrieval** for the **Original Context** sprint.

Sprint goal: Allow users to inspect the original passage and version.

**Deployable:** NON-DEPLOYABLE
**Reason:** Retrieval without navigation/UI is not user-facing.
**Must be deployed with:** F2 and F3

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-F-base` from `main` if it does not already exist.
3. Create `RS-F-base/phase-f1-retrieval` from `RS-F-base`.
4. All work for this phase must be committed on `RS-F-base/phase-f1-retrieval`.
5. Developer merges: `RS-F-base/phase-f1-retrieval` -> `RS-F-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-F / F1 - Retrieval.

Task index source: `TASKS.md`, RSprint section for **RS-F - Original Context**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Application service returns original version metadata and original passage context.
- Load original SectionVersion.HtmlContent where available.
- Use legacy fallback only when no original version exists.

---

## Deliverable

- Original context application service behavior.
- Tests for versioned and legacy fallback retrieval.

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

- Write failing original context retrieval tests.
- Implement retrieval through repositories/application service.
- Return original selected text, context, and version metadata.
- Do not add UI until F3.
---

## Phase-Specific Tests

- Original context loads from original SectionVersion.
- Legacy fallback is explicit and tested.
- Unauthorized user cannot retrieve context.
- Missing original content fails safely.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- Retrieval would use Section.HtmlContent while a SectionVersion exists.
- Original context cannot be authorized through Application.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- Original context loads from original SectionVersion where available.
- Legacy fallback is explicit and tested.
- F1 is not deployed alone.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if original anchor context can be retrieved safely through Application.
