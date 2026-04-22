# DraftView — Agent Instructions

This file defines how any AI coding agent must operate in this repository.

It is tool-agnostic and applies to all agents.

---

# REQUIRED INSTRUCTION FILES

## Always Required

Agents must load and follow:

- .github/Instructions/refactoring.instructions.md

---

## Conditional — Versioning

If the task involves:

- Section
- SectionVersion
- VersioningService
- ImportService
- ISyncProvider
- Publishing workflows
- Reader content resolution

Then the agent must also load and follow:

- .github/Instructions/versioning.instructions.md

These rules are mandatory within their scope.

---

# ARCHITECTURE

The system follows strict layered architecture:

Domain → Application → Infrastructure → Web

Agents must not violate these boundaries.

---

## Domain

- Owns entities, invariants, and factory methods
- No persistence, HTTP, or external services
- All invariants enforced internally

---

## Application

- Owns orchestration and use cases
- Coordinates repositories and services
- No UI logic
- No HTTP concerns

---

## Infrastructure

- Owns persistence and integrations
- Implements interfaces only
- No domain logic

---

## Web

The Web layer is a thin HTTP surface.

### The Web layer must NOT:

- execute business logic
- orchestrate workflows
- loop over domain entities to apply logic
- perform decision-making or branching rules
- coordinate multiple services or repositories
- call repositories directly
- mutate domain entities

### The Web layer must ONLY:

1. Resolve current user or identity
2. Validate input
3. Call an application service
4. Map results to a ViewModel or TempData
5. Return a response

---

# DEVELOPMENT RULES

## Change Discipline

All changes must be:

- minimal
- targeted
- directly related to the task

Agents must not:

- refactor unrelated code
- rename unrelated concepts
- introduce unnecessary abstractions

---

## Behaviour vs Refactor

Refactor and behaviour changes must be separate.

Sequence:

1. Refactor (tests must remain green)
2. Commit
3. Behaviour change
4. Commit

---

## TDD

For Domain, Application, and Infrastructure:

1. Create stub with NotImplementedException
2. Write failing tests
3. Implement until tests pass
4. Run full test suite
5. Refactor safely

Agents must not write production code without tests where required.

---

## Persistence

- All persistence via repositories
- No DbContext outside Infrastructure
- No repository access from Web
- All writes go through Application services

---

## Immutability

Entities representing historical or published state:

- must not be modified after creation
- must not expose setters for core data

Changes must create new records, not mutate existing ones.

---

## Event Handling

User-facing events must:

- be created at the time the event occurs
- be persisted
- not be reconstructed dynamically later

---

## Scope Control

Agents must:

- identify the exact files required
- limit changes to those files

Agents must not expand scope during implementation.

---

# STOP CONDITIONS

Agents must stop and request clarification if:

- requirements are ambiguous
- multiple valid implementation paths exist
- a change crosses architectural boundaries
- a rule conflict occurs

Agents must not proceed based on assumptions.

---

# RULE PRIORITY

If rules conflict:

1. versioning.instructions.md (when applicable)
2. refactoring.instructions.md
3. AGENTS.md
4. other documentation

Higher priority rules override lower ones.

---

# FAILURE CONDITIONS

The following are invalid outputs:

- violating layer boundaries
- Web layer performing application logic
- mixing refactor and behaviour change
- writing code without required tests
- mutating immutable data
- reconstructing persisted events dynamically
- expanding scope beyond the task

If a solution would require violating these rules:

STOP and explain the conflict.

# SOURCE CONTROL — BRANCHING AND COMMIT RULES

All work in this repository must follow strict branching and commit discipline.

---

## 1. Branching Model

All code changes must be made on an appropriate branch.

Agents must not commit directly to `main`, except for explicitly defined chore work.

---

## 2. Branch Types

Work must be performed on one of the following parent branches:

### Bug Fixes
- `BugFix-PC`
- `BugFix-Mac`

### Changes / Features
- `Change-PC`
- `Change-Mac`

### Sprint Work
- `[letter]-Sprint-#`
- Example: `A-Sprint-1`

---

## 3. Sub-Branch Requirement

Every task or prompt must be implemented on a dedicated sub-branch.

### Format

`<parent-branch>/<task-name>`

### Examples

- `BugFix-PC/fix-reader-access-null`
- `Change-Mac/add-notification-persistence`
- `A-Sprint-1/republish-flow`

---

## 4. Branch Rules

Agents must:

- confirm the current branch before making changes
- create a task-specific sub-branch if one does not exist
- perform all work within that sub-branch

Agents must not:

- commit directly to `main` (except chores)
- reuse unrelated branches
- mix multiple tasks into one branch

---

## 5. Chore Exception — Direct Commits to Main

Commits may be made directly to `main` ONLY for chore-type changes.

### Allowed Chores

- Markdown documentation updates (`.md`)
- Project documentation
- Instruction files
- Solution file updates (`.slnx`)
- Non-executable repository metadata

---

### Disallowed in Chores

Chore commits must NOT include:

- `.cs` files
- `.cshtml` files
- `.css` files
- configuration affecting runtime behaviour
- database or migration changes
- any logic, behaviour, or UI changes

---

### Rule

If a change affects system behaviour in any way:

It is NOT a chore.

It must be done on a branch.

---

## 6. Commit Discipline

All commits must:

- be focused and represent a single concern
- align with the branch purpose
- be clearly named using short, imperative messages

### Examples

- `fix: reader access null check`
- `feat: add persisted notifications`
- `docs: update versioning rules`

---

## 7. Refactor vs Behaviour Separation

Refactor and behaviour changes must never be mixed.

Required sequence:

1. Refactor (tests must remain green)
2. Commit (`refactor:` prefix)
3. Behaviour change
4. Commit (`feat:` or `fix:`)

---

## 8. Scope Alignment

Each branch must represent a single concern:

- one bug
- one change
- one prompt task

Branches must not accumulate unrelated work.

---

## 9. Safety Rules

Agents must not:

- perform destructive git operations
- rewrite history
- force push
- merge branches without explicit instruction

---

## 10. Failure Conditions

The following are invalid:

- committing code changes to `main`
- working without a task-specific branch
- mixing unrelated changes in a branch
- mixing refactor and behaviour changes
- misclassifying a change as a chore

If branching or commit context is unclear:

STOP and request clarification before proceeding.