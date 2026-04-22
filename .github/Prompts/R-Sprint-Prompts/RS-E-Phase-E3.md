# RS-E Phase E3 - Relink (Local Execution Phase)

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

Complete **RS-E Phase E3 - Relink** for the **Human Override** sprint.

Sprint goal: Allow users to correct automated relocation.

**Deployable:** NON-DEPLOYABLE
**Reason:** Relink needs integrated status display and selection UI.
**Must be deployed with:** E4

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-E-base` from `main` if it does not already exist.
3. Create `RS-E-base/phase-e3-relink` from `RS-E-base`.
4. All work for this phase must be committed on `RS-E-base/phase-e3-relink`.
5. Developer merges: `RS-E-base/phase-e3-relink` -> `RS-E-base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-E / E3 - Relink.

Task index source: `TASKS.md`, RSprint section for **RS-E - Human Override**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Add domain/application relink operation from a new selected passage.
- Manual relink outranks automated matches.
- Persist actor, timestamp, target version, and selected passage data.

---

## Deliverable

- Relink operation and tests.
- Application DTO for relink selection.

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

- Write failing relink tests.
- Validate new selected passage through canonicalization contract.
- Set current match to UserRelinked/manual method.
- Persist audit metadata.
- Do not add final UI until E4.
---

## Phase-Specific Tests

- Authorized relink creates authoritative current match.
- Invalid relink selection fails.
- Actor and timestamp persist.
- Automated relocation cannot overwrite relink for same target version.
- Unauthorized relink fails.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- Relink cannot reuse validated selection capture.
- Manual relink would be treated as lower authority than automation.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- Relink creates authoritative current match.
- Automated relocation cannot overwrite relink for same target version.
- E3 is not deployed alone.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if users can authoritatively select the correct current passage through Application behavior.
