---
mode: agent
description: S-Sprint-1 Phase 1 - Architecture and task alignment for background Dropbox sync
---

# S-Sprint-1 Phase 1 - Architecture and Task Alignment

## Agent Requirements

Before performing any work, read and apply:

1. `AGENTS.md`
2. `.github/Instructions/refactoring.instructions.md`

This phase is documentation-only. Do not load `.github/Instructions/versioning.instructions.md`
unless the planned change expands beyond `TASKS.md` status/reference updates.

If these files cannot be read or applied, stop and ask for clarification.

## Branching

This task is sprint work, so use a task-specific sub-branch under the sprint parent branch.

1. Confirm the current branch before making changes.
2. Ensure the sprint parent branch is `S-Sprint-1`.
3. Create and work on `S-Sprint-1/phase-1-architecture-task-alignment`.
4. Commit only documentation changes on that task branch.
5. When all Success Gates pass, present merge commands only. Do not merge to `main`.

If `S-Sprint-1` does not exist locally, stop and ask the developer whether to create it
from `main` or another branch.

## Context

This is the first phase of the Dropbox webhook sync series. The goal is to document the
feature in `TASKS.md` and align task tracking before any code is written.

Background Dropbox webhook sync is an ingestion-only feature. It updates working state
but never publishes content or creates versions. See
`DropBox Synchronisation Using WebHooks.md` for full architecture.

## Reading List

Read the following files in order before making changes:

1. `DropBox Synchronisation Using WebHooks.md`
   - Read sections 1-7, from Purpose through State Model.
   - Ask: What is the scope of webhook sync? What does it explicitly not do?

2. `TASKS.md`
   - Read section 0, Active Work.
   - Read section 1, Reference Documents.
   - Read section 3.1, Dropbox Webhook Sync Sprint Series.
   - Ask: Is S-Sprint-1 Phase 1 unstarted, in progress, or complete?

3. `.github/PROMPT-STANDARD.md`
   - Read the Overview, Sprint Phase Prompt Structure, and Anti-Patterns sections.
   - Ask: What completion evidence should be captured for a prompt-driven docs phase?

## Specification

### Task 1: Update TASKS.md

Mark S-Sprint-1 Phase 1 as in progress without using `[x]`.

The checkbox must remain unchecked because `TASKS.md` uses `[ ]` for incomplete work and
`[DONE]` only for completed work. Append a brief in-progress note to the Phase 1 line.

Use this exact target wording:

```markdown
  - [ ] Phase 1: Architecture and task alignment — In progress, task tracking alignment started
```

Do not modify any other checkbox in `TASKS.md`.

### Task 2: Verify Reference Documents

Ensure `DropBox Synchronisation Using WebHooks.md` is listed in section 1, Reference
Documents, of `TASKS.md`.

If it is already present, do not modify the reference table.

If it is missing, add this exact row:

```markdown
| `DropBox Synchronisation Using WebHooks.md` | Webhook-driven background Dropbox sync — control model, cursor-based interrogation, S-Sprint series |
```

### Task 3: Add Active Work Status

Update the Active Work summary table in `TASKS.md` section 0 to include this exact row:

```markdown
| S-Sprint Series | 🟡 In progress — S-Sprint-1 Phase 1 |
```

Place it with the other sprint-series rows, after `MT-Sprint Series` and before bug-fix rows.

## What to Produce - Plan First, Then Pause

After reading all files, produce a written plan containing all four sections below.

Stop after the plan. Do not make any changes. Wait for the plan to be reviewed and
approved by the developer.

### Section 1 - Current State Analysis

State precisely:

- Current checkbox and wording for S-Sprint-1 Phase 1 in `TASKS.md`
- Whether `DropBox Synchronisation Using WebHooks.md` is already listed in Reference Documents
- Whether the Active Work table already mentions the S-Sprint series
- Current branch and whether the required task branch exists

### Section 2 - Proposed Changes

List each proposed change in plain English:

- What text will be added or modified in `TASKS.md`
- Confirmation that the Phase 1 checkbox remains `[ ]`
- Exact wording for any new entries
- Whether any reference-table change is needed

### Section 3 - Verification Steps

Describe how you will verify:

- `git diff --check` passes
- The `TASKS.md` diff is limited to the intended Active Work and S-Sprint-1 Phase 1 changes,
  plus the reference document row only if it was missing
- No existing checkboxes changed except the S-Sprint-1 Phase 1 line wording
- No unintended section ordering or formatting changes were introduced

### Section 4 - Success Gates

Use these gates for this documentation-only phase.

**Gate 1 - Plan approved**
- [ ] Developer approved the written plan before file changes began.

**Gate 2 - Changes validated**
- [ ] `TASKS.md` modified correctly; paste the focused diff summary.
- [ ] The Phase 1 checkbox remains `[ ]` with the required in-progress note.
- [ ] No unrelated checkboxes changed.

**Gate 3 - No formatting regressions**
- [ ] `git diff --check` passes.
- [ ] Manual diff review confirms no unintended formatting changes.

**Gate 4 - Committed to task branch**
- [ ] Changes committed to `S-Sprint-1/phase-1-architecture-task-alignment` with message:
  `docs: mark S-Sprint-1 Phase 1 in progress`
- [ ] `git status` is clean.

**Gate 5 - Present merge commands**
- [ ] Present for manual execution. Do not execute:

```bash
git checkout S-Sprint-1
git merge S-Sprint-1/phase-1-architecture-task-alignment
git checkout main
git merge S-Sprint-1
git push origin main
```

## Rules

- No code changes in this phase; documentation updates only.
- Modify only `TASKS.md`.
- Do not modify any checkbox except the S-Sprint-1 Phase 1 line wording.
- Do not use `[x]`; completed work uses `[DONE]` only.
- Do not change the structure or ordering of `TASKS.md` sections.
- Do not merge branches automatically.
- A task is not complete until every Success Gate is confirmed.
