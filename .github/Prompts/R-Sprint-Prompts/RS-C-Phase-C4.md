# RS-C Phase C4 â€” Tests (Local Execution Phase)

## Execution Mode
Local Execution Phase

Apply the **Test Execution Override - Local Phases** rules from `AGENTS.md`.

## Required Reading Order
1. `AGENTS.md`
2. `TASKS.md`
3. `Passage Anchoring, Reader Continuity, and Inline Commentary.md`
4. `PRINCIPLES.md`
5. `REFACTORING.md`

Do not proceed until these documents have been read.

---

## Objective

Complete **RS-C Phase C4 â€” Tests** for the **Inline Comments** sprint.

**Deployable:** âœ… Yes

---

## Phase Source of Truth

From `TASKS.md`:

> **RS-C â€” Inline Comments**
> - Phase C4 â€” Tests

From `Passage Anchoring, Reader Continuity, and Inline Commentary.md`:

> [TODO: Extract exact intent/constraints from source doc]

These instructions are mandatory.

---

## Scope

[TODO: Define exact scope â€” what must be built, what must not be touched]

---

## Deliverable

[TODO: Define expected output â€” tests? entities? services? migrations? UI?]

---

## Hard Constraints

### TDD Rules (if applicable)
- Write failing tests first
- Confirm red before implementation
- Confirm green after implementation
- Full suite must pass

### Architecture
Respect layered architecture:

- Domain owns rules and invariants
- Application owns orchestration
- Infrastructure owns persistence
- Web reflects behaviour and does not own business rules

### Production Safety
**Deployable:** âœ… Yes

---

## Test Execution Rules

Apply the **Test Execution Override - Local Phases** rules from `AGENTS.md`.

---

## Required Implementation Steps

[TODO: Define exact implementation sequence]

---

## Stop Conditions

Stop immediately and report if any of the following occur:

- [TODO: Define phase-specific stop conditions]

---

## Definition of Done

This phase is done only when:

- [TODO: Define completion criteria]

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if it [TODO: state success condition].
