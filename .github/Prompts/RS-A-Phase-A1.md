# RS-A Phase A1 — Passage Anchoring Model Discovery (Cloud Execution Phase)

## Execution Mode
Cloud Execution Phase

Apply the **Test Execution Override — Cloud Phases** rules from `AGENTS.md`.

## Required Reading Order
1. `AGENTS.md`
2. `TASKS.md`
3. `Passage Anchoring, Reader Continuity, and Inline Commentary.md`
4. `PRINCIPLES.md`
5. `REFACTORING.md`

Do not proceed until these documents have been read.

---

## Objective

Complete **RS-A Phase A1 — Model Discovery** for the RSprint series.

This phase is a **discovery and design proposal phase**, not an implementation phase.

The goal is to inspect the current DraftView solution and propose the **minimum viable anchor model** needed to support:

- inline selected-text comments
- reader resume continuity
- future cross-version relocation
- original context retrieval
- future human relink and reject actions

This phase must remain production-safe and cloud-safe.

---

## Phase Source of Truth

From `TASKS.md`:

> **RS-A — Anchor Foundation**
> - Phase A1 — Model discovery (Copilot-led inspection and proposal)

From `Passage Anchoring, Reader Continuity, and Inline Commentary.md`:

> Introduce anchor capability without breaking existing comments or reading.

> Inspect Comment, ReadEvent, SectionVersion

> Propose anchor model

> Output: Proposal only

> Stop after proposal.

These instructions are mandatory.

---

## Scope

Inspect the existing codebase and identify the current structures, seams, and constraints relevant to passage anchoring.

You must inspect, at minimum, the current equivalents of:

- comment entity/model
- read event entity/model
- versioned section/document model
- comment creation flow
- reader resume/progress flow
- services and repositories directly involved in those areas
- any existing UI/view models that materially constrain anchor design

You may inspect additional files if required, but keep scope tight and relevant.

---

## Deliverable

Produce a **discovery proposal**, not code implementation.

The output must include:

1. A concise summary of the current model and where anchoring naturally fits
2. The minimum viable anchor design options found from the existing codebase
3. A recommended approach consistent with current architecture
4. Exact files and classes likely to be affected in later phases
5. Risks, unknowns, and structural decisions requiring approval before implementation
6. A clear recommendation for the next implementation phase boundary

The output must be committed in one of these forms:

- a markdown design summary added to the working branch, or
- a pull request description containing the full proposal

Prefer a markdown design summary in the repository if an appropriate planning/docs location already exists. Do not invent a new documentation structure unless the repository already contains a suitable pattern.

---

## Hard Constraints

### Discovery Only
Do not implement the anchor model in this phase.

Do not:
- add entities
- add properties
- add migrations
- modify controllers
- modify services
- modify views
- modify tests except where needed to support discovery notes already established in repo patterns

This phase ends at proposal.

### No Guessing
Do not guess:
- file structure
- intended entity shape
- existing ownership boundaries
- whether anchor data should be embedded or separate

All findings must come from the current repository state.

### Architecture
Respect layered architecture:

- Domain owns rules and invariants
- Application owns orchestration
- Infrastructure owns persistence
- Web reflects behaviour and does not own business rules

### Production Safety
This phase must be safe for production deployment.

That means:
- no behavioural change
- no schema change
- no user-visible change
- no incomplete feature exposure

---

## Cloud Execution Rules

This is a cloud execution phase.

Run only tests that do **not** require:

- database access
- application startup
- browser automation
- external services

If safe completion of this phase requires any excluded validation, stop and report the exact blocker.

Because this is a discovery-only phase, no database-backed or startup-backed validation should be necessary.

---

## Required Investigation Questions

Your proposal must answer these questions from the actual repository state:

1. Where should passage anchoring live most naturally in the current model?
2. Should the anchor concept be:
   - embedded into existing models, or
   - introduced as a separate structure?
3. What existing model best represents the canonical source version for an anchor?
4. What current comment flow would need to change to support selected-text comments later?
5. What current reader progress/resume flow would need to change to support anchored resume later?
6. What existing abstractions can be reused instead of introducing parallel systems?
7. What is the smallest safe implementation sequence for RS-A2 onward?

---

## Expected Output Structure

Use this exact structure in the final discovery summary:

### RS-A Phase A1 — Discovery Summary

#### 1. Current Relevant Structures
List the exact files, classes, and responsibilities discovered.

#### 2. Existing Natural Seams
Identify where anchoring could be introduced with least disruption.

#### 3. Minimum Viable Anchor Model Options
Describe the viable options supported by the current codebase.

#### 4. Recommended Approach
State the recommended option and why it best fits DraftView.

#### 5. Likely Future Change Set
List the exact files/classes likely to change in RS-A2, RS-A3, and RS-A4.

#### 6. Risks and Approval Decisions
State what must be confirmed before implementation starts.

#### 7. Next Phase Recommendation
Define the correct scope for RS-A2.

---

## Stop Conditions

Stop immediately and report if any of the following occur:

- required repository content is missing
- the current model is inconsistent enough that no safe recommendation can be made
- implementation would be required to answer the discovery questions
- full-environment validation would be required
- the phase would drift beyond discovery into design-by-assumption

Do not cross from discovery into implementation.

---

## Definition of Done

This phase is done only when:

- the current relevant model has been inspected
- a repository-grounded proposal has been produced
- the recommendation is architecture-consistent
- no implementation has been performed
- the result is safe for production deployment
- the output clearly defines the approval point before RS-A2

---

## Final Instruction

Be precise, conservative, and architecture-led.

This phase is successful only if it reduces ambiguity for the next phase without introducing any code or structural guesswork.
