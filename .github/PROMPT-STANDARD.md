# DraftView — Prompt File Standard
Version: 1.0 | Date: 2026-04-20

---

## Overview

Prompt files are Copilot agent instructions stored in `.github/Prompts/`. They define a complete, self-contained specification for a single unit of work — a bug fix, a sprint phase, or a one-time task. Every prompt file follows this standard.

Prompt files exist because:
- They encode the full context an agent needs without relying on conversation history
- They enforce the plan-and-pause gate — no code before the plan is reviewed
- They define success explicitly so "done" is unambiguous
- They are version-controlled alongside the code they describe

---

## File Naming

| Type | Pattern | Example |
|------|---------|---------|
| Bug fix | `BUG-{N}-{kebab-description}.prompt.md` | `BUG-009-new-scene-not-appearing-after-sync.prompt.md` |
| VSprint phase | `vsprint{N}-phase{N}.prompt.md` | `vsprint6-phase1.prompt.md` |
| RSprint phase | `rsprint{N}-phase{N}.prompt.md` | `rsprint1-phase1.prompt.md` |
| MT-Sprint phase | `mtsprint{N}-phase{N}.prompt.md` | `mtsprint1-phase1.prompt.md` |
| One-time task | `{kebab-description}.prompt.md` | `production-reset.prompt.md` |

---

## Front Matter

Every prompt file must begin with:

```
---
mode: agent
description: {BUG-N or Sprint reference} — {one-line description}
---
```

---

## Required Sections

Every bug fix prompt must contain all of the following sections in this order.

### 1. Title
`# BUG-{N} — {Description}`

### 2. Branching
Explicit branch strategy:
1. Checkout `BugFix-PC` (or `BugFix-Mac`) and pull latest from `main`
2. Create `bugfix/BUG-{N}-{description}` from `BugFix-PC`
3. All work on the feature branch
4. When all Success Gates pass, present merge commands — do not execute
5. Developer merges: `bugfix/BUG-{N}-...` → `BugFix-PC` → `main`

### 3. Symptoms
Numbered list of exactly what the user observes. No root cause analysis here — symptoms only. Include any relevant log output, error messages, or screenshots descriptions.

### 4. Where to Start Looking
Ordered reading list — files only, no analysis. Each entry states:
- The file path
- What specific aspect to read
- What question to ask while reading

Do not hand Copilot the root cause. It must reason to it from the reading list.

### 5. What to Produce — Plan First, Then Pause
Instruct Copilot to produce a written plan with all four sections below, then **stop and wait for approval** before writing any code.

#### Section 1 — Root Cause Analysis
State precisely what is wrong and why. No guessing — evidence from the reading list only.

#### Section 2 — Failing Test Plan
For each test: class name, method name, arrange/act/assert, why red before fix, why green after.

#### Section 3 — Proposed Fix
Plain English description of the fix. State what changes, what doesn't, whether a migration is needed.

#### Section 4 — Success Gates
Seven explicit gates, all must be confirmed before declaring done:

| Gate | Condition |
|------|-----------|
| 1 | New tests confirmed red — paste failing output |
| 2 | New tests confirmed green — paste passing output |
| 3 | Full suite passes with zero regressions — paste count |
| 4 | Browser verified by developer |
| 5 | Committed to feature branch with correct message |
| 6 | TASKS.md updated to `[DONE]` with date and summary |
| 7 | Production deployed and UAT validated — merge commands presented, not executed |

### 6. Rules
Non-negotiable constraints that apply to every prompt:
- No production code changes until the plan is reviewed and approved
- TDD: failing test → confirm red → fix → confirm green
- No null guards, defensive try/catch, or workarounds — fix the root cause
- Existing tests must not be modified to make new tests pass
- All git commands are presented to the developer for manual execution — never executed automatically
- A task is not complete until every Success Gate is confirmed

---

## Sprint Phase Prompt Structure

Sprint phase prompts follow a slightly different structure:

### Required Sections
1. **Title** — `# {Sprint} Phase {N} — {Description}`
2. **Branching** — same pattern as bug fixes
3. **Context** — brief summary of what this phase builds on
4. **Reading List** — files to read before writing code
5. **Specification** — what to build, domain rules, invariants
6. **TDD Sequence** — explicit stub → failing test → implementation steps
7. **Success Gates** — same 7-gate structure as bug fixes
8. **Rules** — same non-negotiable rules as bug fixes

---

## Anti-Patterns (never do these)

- **Don't hand Copilot the root cause** — symptoms and a reading list only; it must reason to the answer
- **Don't skip the plan gate** — code written before the plan is reviewed is always wrong
- **Don't omit Success Gates** — "tests pass" is not a gate; paste the count
- **Don't automate git** — all git commands are manual; present them, never execute
- **Don't mix concerns** — one prompt per bug, one prompt per sprint phase
- **Don't use `[x]`** — always use `[DONE]` for completed items in TASKS.md

---

## Example: Minimal Bug Fix Prompt

```markdown
---
mode: agent
description: BUG-XXX — Short description
---

# BUG-XXX — Short Description

## Branching
1. Checkout `BugFix-PC` and pull latest from `main`
2. Create `bugfix/BUG-XXX-short-description` from `BugFix-PC`
3. All work on `bugfix/BUG-XXX-short-description`
4. When all Success Gates pass, present merge commands — do not execute
5. Developer merges: `bugfix/BUG-XXX-...` → `BugFix-PC` → `main`

## Symptoms
1. ...
2. ...

## Where to Start Looking
1. `Path/To/File.cs` — read X, ask Y
2. `Path/To/Other.cs` — read X, ask Y

## What to Produce — Plan First, Then Pause
Produce a plan with all four sections. Stop after the plan. Wait for approval.

### Section 1 — Root Cause Analysis
### Section 2 — Failing Test Plan
### Section 3 — Proposed Fix
### Section 4 — Success Gates
[standard 7-gate checklist]

## Rules
[standard rules block]
```
