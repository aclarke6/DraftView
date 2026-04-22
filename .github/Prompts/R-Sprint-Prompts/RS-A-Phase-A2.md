# RS-A Phase A2 - Domain Definition (TDD) (Local Execution Phase)

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

Complete **RS-A Phase A2 - Domain Definition (TDD)** for the **Anchor Foundation** sprint.

Sprint goal: Introduce anchor model, persistence, and application surface without changing UI behavior.

**Deployable:** NON-DEPLOYABLE
**Reason:** Domain model has no persistence until A3.
**Must be deployed with:** A3

---

## Branching

1. Checkout `main` and pull latest from `origin/main`.
2. Create `RS-A/base` from `main` if it does not already exist.
3. Create `RS-A/phase-a2-domain-definition` from `RS-A/base`.
4. All work for this phase must be committed on `RS-A/phase-a2-domain-definition`.
5. Developer merges: `RS-A/phase-a2-domain-definition` -> `RS-A/base` -> `main`.

---

## Phase Source of Truth

Primary source: `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 10 / RS-A / A2 - Domain Definition (TDD).

Task index source: `TASKS.md`, RSprint section for **RS-A - Anchor Foundation**.

These instructions are mandatory. If this prompt conflicts with the source document, follow the source document and stop to report the conflict before changing code.

---

## Scope

- Add domain entity/value objects/enums for anchors and matches.
- Use the A1-selected names and files from `Passage Anchoring, Reader Continuity, and Inline Commentary.md`, Section 3.1.1.
- Add nullable `Comment.PassageAnchorId` and nullable `ReadEvent.ResumeAnchorId` in the domain model.
- Add domain methods for creation, match update, orphaning, rejection, and relink.
- Do not add EF mappings, migrations, application services, or Web usage.

---

## Deliverable

- Domain tests written before implementation.
- Minimal domain implementation for PassageAnchor, snapshot, match, status, purpose, and match method concepts.
- Domain-level nullable anchor references on Comment and ReadEvent that preserve existing null-anchor flows.

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

- Create stubs that throw NotImplementedException where needed.
- Create the exact domain files selected by A1:
  - `DraftView.Domain/Entities/PassageAnchor.cs`
  - `DraftView.Domain/ValueObjects/PassageAnchorSnapshot.cs`
  - `DraftView.Domain/ValueObjects/PassageAnchorMatch.cs`
  - `DraftView.Domain/Enumerations/PassageAnchorPurpose.cs`
  - `DraftView.Domain/Enumerations/PassageAnchorStatus.cs`
  - `DraftView.Domain/Enumerations/PassageAnchorMatchMethod.cs`
  - `DraftView.Domain/Interfaces/Repositories/IPassageAnchorRepository.cs`
- Write failing domain tests for invariants and transitions.
- Implement the smallest domain model that satisfies those tests.
- Run required tests and keep behavior changes out of Web/Application/Infrastructure.
---

## Phase-Specific Tests

- Create anchor with valid snapshot succeeds.
- Existing comment factory methods still create comments with null `PassageAnchorId`.
- Existing read-event factory/update paths still work with null `ResumeAnchorId`.
- Read event resume anchor can be updated and cleared.
- Empty selected text throws.
- End offset before start offset throws.
- Original snapshot cannot be mutated through domain API.
- Confidence outside 0-100 throws.
- Orphan transition clears active current match.
- User rejection requires actor id.
- Manual relink requires actor id and valid target.
- Automated match cannot overwrite manual relink for same target version.
---

## Stop Conditions

Stop immediately and report if any of the following occur:

- The domain model needs persistence behavior to express invariants.
- A required invariant conflicts with existing domain conventions.
- A proposed transition would allow user relink to be overwritten by automation.
- A phase requires changing reader content resolution away from SectionVersion.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
---

## Definition of Done

This phase is done only when:

- Domain tests pass.
- No persistence or Web changes are included.
- A2 remains grouped with A3 for deployment.
- `git diff --check` passes for changed files.
- No unrelated files are changed.
- Any test suite required by this phase has passed, or the reason it could not be run is documented.

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if the anchor domain model exists with tested invariants and no persistence or UI behavior.
