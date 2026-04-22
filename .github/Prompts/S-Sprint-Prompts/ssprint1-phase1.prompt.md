---
mode: agent
description: S-Sprint-1 Phase 1 — Architecture and task alignment for background Dropbox sync
---

# S-Sprint-1 Phase 1 — Architecture and Task Alignment

## Branching
1. Checkout `main` and pull latest from `origin/main`
2. Create `ssprint/S-Sprint-1-Phase-1-architecture` from `main`
3. All work on `ssprint/S-Sprint-1-Phase-1-architecture`
4. When all Success Gates pass, present merge commands — do not execute
5. Developer merges: `ssprint/S-Sprint-1-Phase-1-architecture` → `main`

## Context
This is the first phase of the Dropbox webhook sync series. The goal is to document the feature in `TASKS.md` and ensure all architecture documents are aligned before any code is written.

Background Dropbox webhook sync is an ingestion-only feature. It updates working state but never publishes content or creates versions. See `DropBox Synchronisation Using WebHooks.md` for full architecture.

## Reading List
Read the following files in order before making changes:

1. `DropBox Synchronisation Using WebHooks.md`
   - Read sections 1–7 (Purpose through State Model)
   - Ask: What is the scope of webhook sync? What does it explicitly NOT do?

2. `TASKS.md`
   - Read section 3.1 (Dropbox Webhook Sync Sprint Series)
   - Ask: Is S-Sprint-1 Phase 1 marked as started or complete?

3. `.github/copilot-instructions.md`
   - Read the section on documentation updates
   - Ask: What level of detail is expected when updating TASKS.md?

## Specification

### Task 1: Update TASKS.md
Mark S-Sprint-1 Phase 1 as **in progress** by changing the checkbox state and adding a brief note indicating work has started.

### Task 2: Verify Reference Documents
Ensure `DropBox Synchronisation Using WebHooks.md` is listed in section 1 (Reference Documents) of `TASKS.md`. If not present, add it.

### Task 3: Add Active Work Status
Update the Active Work summary table in TASKS.md section 0 to include:
```
| S-Sprint Series | 🟡 In progress — S-Sprint-1 Phase 1 |
```

## What to Produce — Plan First, Then Pause

After reading all files, produce a written plan containing all four sections below.

Stop after the plan. Do not make any changes. Wait for the plan to be reviewed and approved by the developer.

### Section 1 — Current State Analysis
State precisely:
- Current checkbox state of S-Sprint-1 Phase 1 in TASKS.md
- Whether `DropBox Synchronisation Using WebHooks.md` is already listed in Reference Documents
- Whether Active Work table mentions S-Sprint series

### Section 2 — Proposed Changes
List each change in plain English:
- What text will be added or modified in TASKS.md
- What the checkbox state will become
- Exact wording for any new entries

### Section 3 — Verification Steps
Describe how you will verify:
- TASKS.md is syntactically valid markdown after changes
- All existing checkboxes remain unchanged except S-Sprint-1 Phase 1
- No unintended formatting changes

### Section 4 — Success Gates

**Gate 1 — Changes validated**
- [ ] TASKS.md modified correctly — paste diff summary

**Gate 2 — No regressions**
- [ ] Markdown validates with no warnings

**Gate 3 — Committed to GitHub**
- [ ] Committed to `ssprint/S-Sprint-1-Phase-1-architecture` with message:
    `docs: S-Sprint-1 Phase 1 — mark architecture and task alignment in progress`
- [ ] `git status` is clean

**Gate 4 — Present merge commands**
- [ ] Present for manual execution — do not execute:
  ```
  git checkout main
  git merge ssprint/S-Sprint-1-Phase-1-architecture
  git push origin main
  ```

## Rules
- No code changes in this phase — documentation updates only
- Do not modify any checkboxes in TASKS.md except S-Sprint-1 Phase 1
- Do not change the structure or ordering of TASKS.md sections
- All git commands are presented to the developer for manual execution — never executed automatically
- A task is not complete until every Success Gate is confirmed
