# AGENT REQUIREMENT - MANDATORY

Before performing any work, the agent MUST:

1. Read and apply:
   - AGENTS.md
   - .github/Instructions/refactoring.instructions.md

2. Operate fully within their constraints

If these files are not read or cannot be applied:

STOP.

Do not proceed with the task.

---
mode: agent
description: S-Sprint-6 Phase 2 - Batch selection and bounded processing
---

# S-Sprint-6 Phase 2 - Batch selection and bounded processing

## Branching
1. Checkout `main` and pull latest from `origin/main`
2. Create `ssprint/S-Sprint-6-Phase-2-batch-selection-and-bounded-processing` from `main`
3. All work on `ssprint/S-Sprint-6-Phase-2-batch-selection-and-bounded-processing`
4. When all Success Gates pass, present merge commands - do not execute
5. Developer merges: `ssprint/S-Sprint-6-Phase-2-batch-selection-and-bounded-processing` → `main`

## Context
This is Phase 2 of S-Sprint-6 (Periodic Worker and Held Request Recovery).

See `DropBox Synchronisation Using WebHooks.md` section for S-Sprint-6 for full phase architecture.

**Phase Brief:** Implement bounded query selection and safe loop processing so worker load remains predictable.

## Reading List
Read the following files in order before writing code:

1. `DropBox Synchronisation Using WebHooks.md`
   - Read S-Sprint-6 Phase 2 section
   - Ask: What is the specific deliverable for this phase?

2. `TASKS.md`
   - Read section 3.1 (Dropbox Webhook Sync Sprint Series)
   - Ask: What is the current phase completion status?

3. `.github/copilot-instructions.md`
   - Read TDD and phased delivery rules
   - Ask: What constraints apply to this phase?

## Specification

### Phase Goal
Implement bounded query selection and safe loop processing so worker load remains predictable.

### Key Deliverables
Refer to `DropBox Synchronisation Using WebHooks.md` S-Sprint-6 Phase 2 for:
- Exact implementation requirements
- Domain/application/infrastructure scope
- Test coverage expectations

### What NOT to Do
- Do not skip TDD sequence (stub → failing test → implementation)
- Do not add features beyond this phase's scope
- Do not modify unrelated code
- Do not publish content or create versions (ingestion-only constraint)

## What to Produce - Plan First, Then Pause

After reading all files, produce a written plan containing all four sections below.

Stop after the plan. Do not write any code. Wait for the plan to be reviewed and approved by the developer.

### Section 1 - Current State Analysis
State precisely:
- What code/tests exist from previous phases
- What dependencies this phase has on prior work
- Current checkbox state in TASKS.md

### Section 2 - Implementation Plan
Describe in plain English:
- What files will be created or modified
- What tests will be written (arrange/act/assert)
- What the TDD sequence is (stub → red → green)

### Section 3 - Verification Steps
Confirm:
- How you will verify the phase deliverable works
- What browser/manual testing is needed (if applicable)
- How you will confirm no regressions

### Section 4 - Success Gates

**Gate 1 - Tests written and confirmed red (if TDD phase)**
- [ ] All new tests written
- [ ] Tests confirmed red - paste failing output

**Gate 2 - Implementation complete and tests green**
- [ ] Implementation complete
- [ ] All new tests pass - paste passing output

**Gate 3 - No regressions**
- [ ] Full test suite passes - paste count

**Gate 4 - Browser verification (if applicable)**
- [ ] Manual verification complete (describe what was verified)

**Gate 5 - Committed to GitHub**
- [ ] Committed to `ssprint/S-Sprint-6-Phase-2-batch-selection-and-bounded-processing` with message:
    `feat: S-Sprint-6 Phase 2 - Batch selection and bounded processing`
- [ ] `git status` is clean

**Gate 6 - TASKS.md updated**
- [ ] S-Sprint-6 Phase 2 marked complete in TASKS.md
- [ ] Committed with message: `chore: mark S-Sprint-6 Phase 2 complete in TASKS.md`

**Gate 7 - Present merge commands**
- [ ] Present for manual execution - do not execute:
  ```
  git checkout main
  git merge ssprint/S-Sprint-6-Phase-2-batch-selection-and-bounded-processing
  git push origin main
  ```

## Rules
- No code before the plan is reviewed and approved
- TDD: stub → failing test → implementation → confirm green
- Follow existing patterns in Domain/Application/Infrastructure layers
- No inline styles in views - CSS classes only
- Webhook sync is ingestion-only - never publishes or creates versions
- All git commands are presented to the developer for manual execution
- A task is not complete until every Success Gate is confirmed

