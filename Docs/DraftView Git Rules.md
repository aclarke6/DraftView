# DraftView — Git Project Plan: Rules and Gates

---

## 1. Branch Hierarchy

```
main
└── vsprint-{N}
    └── vsprint-{N}--phase-{P}-{slug}
```

Every branch has exactly one parent. Phase branches are cut from their sprint branch. Sprint branches are cut from main. No exceptions.

**Important:** Phase branches use a double-dash (`--`) separator between the sprint prefix and the phase segment. A forward slash cannot be used because Git treats `vsprint-1` as a directory prefix in refs, which prevents creating a branch named `vsprint-1` once any `vsprint-1/...` branch exists. This was discovered during V-Sprint 1 Phase 1.

```
main
└── vsprint-1
    ├── vsprint-1--phase-1-domain-infrastructure
    ├── vsprint-1--phase-2-tree-service-import
    ├── vsprint-1--phase-3-versioning-service
    ├── vsprint-1--phase-4-reader-content-source
    └── vsprint-1--phase-5-author-ui-manual-upload
```

---

## 2. Branch Naming

| Level | Pattern | Example |
|-------|---------|---------|
| Sprint | `vsprint-{N}` | `vsprint-1` |
| Phase | `vsprint-{N}--phase-{P}-{slug}` | `vsprint-1--phase-2-tree-service-import` |
| Hotfix | `hotfix/{slug}` | `hotfix/fix-sync-null-reference` |
| Debt | `debt/{slug}` | `debt/remove-section-htmlcontent-fallback` |

**Slug rules:**
- Lowercase, hyphen-separated
- Describes the deliverable, not the ticket number
- 3–6 words maximum
- Never includes the word "wip" or "temp"

---

## 3. Branch Lifecycle

### 3.1 Starting a Sprint

1. Confirm `main` is clean — all prior work merged, CI green
2. Cut sprint branch from `main`:
   `git checkout main && git pull && git checkout -b vsprint-{N}`
3. Push sprint branch immediately:
   `git push -u origin vsprint-{N}`
4. Do not commit directly to the sprint branch — it receives merges only

### 3.2 Starting a Phase

1. Confirm the previous phase PR is merged into the sprint branch
2. Pull the sprint branch:
   `git checkout vsprint-{N} && git pull`
3. Cut phase branch from sprint branch:
   `git checkout -b vsprint-{N}/phase-{P}-{slug}`
4. Push phase branch immediately:
   `git push -u origin vsprint-{N}/phase-{P}-{slug}`

### 3.3 During a Phase

- Commit frequently — every green test checkpoint is a commit
- Commit messages follow the rules in section 5
- No WIP commits on pushed branches — squash locally before pushing if needed
- Never commit directly to `vsprint-{N}` or `main`

### 3.4 Completing a Phase

The phase is complete when all gates in section 6 are satisfied. Raise a PR from the phase branch into the sprint branch. Merge only after gates pass.

### 3.5 Completing a Sprint

The sprint is complete when all phases are merged into the sprint branch and all gates in section 7 are satisfied. Raise a PR from the sprint branch into `main`. Merge only after gates pass.

### 3.6 Hotfixes

A production defect discovered during a sprint does not block the sprint, but it must not be absorbed into sprint work.

1. Cut `hotfix/{slug}` from `main` (not from the sprint branch)
2. Fix, test, merge hotfix PR into `main`
3. Immediately rebase the active sprint branch onto `main`:
   `git checkout vsprint-{N} && git rebase main`
4. Rebase the active phase branch onto the updated sprint branch:
   `git checkout vsprint-{N}/phase-{P}-{slug} && git rebase vsprint-{N}`
5. Resolve any conflicts before continuing sprint work

A hotfix is never merged into a phase or sprint branch directly. It always goes to `main` first.

---

## 4. Commit Rules

### 4.1 When to Commit

- Every time a new test goes green
- Every time a stub is replaced with a passing implementation
- Every time a migration is generated
- Every time a refactor leaves tests green
- At the end of every working session, regardless of completeness — but mark incomplete work clearly (see 4.3)

### 4.2 Commit Message Format

```
{type}: {imperative summary}

{optional body — why, not what}
```

**Type prefixes:**

| Prefix | Use |
|--------|-----|
| `feat` | New behaviour visible to author or reader |
| `test` | Test added or fixed (no production code change) |
| `domain` | Domain entity or invariant change |
| `infra` | Repository, migration, EF configuration |
| `app` | Application service change |
| `web` | Controller, view, CSS change |
| `refactor` | Structure improved, behaviour unchanged |
| `fix` | Bug fix — production code only |
| `debt` | Known debt addressed |
| `chore` | Config, tooling, project file |

**Summary rules:**
- Imperative mood: "Add SectionVersion entity" not "Added" or "Adding"
- 60 characters maximum
- No trailing full stop
- Specific enough to stand alone in a log

**Examples:**

```
domain: Add SectionVersion entity with Create factory method
test: Prove SectionVersion rejects Folder node type
infra: Generate AddVersioningAndManualUpload migration
app: Implement VersioningService.RepublishChapterAsync
web: Add Republish button to Author/Sections view
fix: Correct ContentHash comparison in ImportService
refactor: Extract version number resolution to private method
debt: Remove Section.HtmlContent fallback from reader view
```

### 4.3 Incomplete Work

If a session ends mid-phase, commit with a `wip:` prefix on the local branch only. Before pushing, squash the WIP commit into a clean commit:

```
git add .
git commit -m "wip: halfway through ImportService tests"
# ... next session ...
git add .
git commit --amend -m "app: Implement ImportService with RtfImportProvider"
```

Never push a `wip:` commit to origin.

---

## 5. Pull Request Rules

### 5.1 PR Title

Matches the format: `{Sprint} Phase {N} — {description}`

Examples:
- `V-Sprint 1 Phase 2 — Section Tree Service and Import Provider`
- `V-Sprint 1 — Core Versioning Backbone and Manual Upload`
- `Hotfix — Fix sync null reference on empty binder`

### 5.2 PR Description Template

Every PR must include:

```
## What this delivers
{1–3 sentences describing the deliverable, not the implementation}

## Phase gates satisfied
- [ ] All new tests green
- [ ] No regressions (test count: {N} → {N})
- [ ] Migration applied cleanly (if applicable)
- [ ] Browser verified (if applicable)
- [ ] TASKS.md updated

## Known limitations / follow-up
{anything deliberately deferred or left for the next phase}
```

### 5.3 PR Scope

- One phase per PR (phase → sprint branch)
- One sprint per PR (sprint branch → main)
- A PR that mixes a refactor with a behaviour change is rejected — split it
- A PR that contains a migration must contain the feature that requires it — never a migration alone, never a feature without its migration

---

## 6. Phase Gate — Required Before Phase PR Merges

All of the following must be true. Any single failure is a blocker.

| Gate | Requirement |
|------|-------------|
| **G1 — Tests green** | All new tests pass. Zero failures. |
| **G2 — No regression** | Total passing test count is equal to or greater than before the phase began. A drop in count is a blocker, not a footnote. |
| **G3 — Migration clean** | If the phase includes a migration: migration applies cleanly from the current production schema. Migration is committed in the same batch as the feature code that requires it. |
| **G4 — No dead code** | No commented-out code, unused methods, or unreachable branches introduced. |
| **G5 — No inline styles** | No `style=""` attributes introduced in any Razor view. All new CSS in the appropriate stylesheet with a component comment. |
| **G6 — No magic strings** | No string or numeric literals that appear more than once or whose meaning is not self-evident. Named constants or enum values used instead. |
| **G7 — TASKS.md updated** | Phase item marked complete. Any newly discovered work added. |
| **G8 — Browser verified** | If the phase includes any view change: manually verified in the browser. Both author and reader paths exercised where applicable. |

---

## 7. Sprint Gate — Required Before Sprint PR Merges to Main

All phase gates must already be satisfied. Additionally:

| Gate | Requirement |
|------|-------------|
| **S1 — All phases merged** | All phase branches for the sprint are merged into the sprint branch. No open phase PRs. |
| **S2 — Full test suite green** | `dotnet test` run against the sprint branch with zero failures. Test count pasted into sprint PR description. |
| **S3 — Production migration dry-run** | All migrations for the sprint applied against a copy of the production schema without error. |
| **S4 — UAT complete** | Each deliverable in the sprint has been manually verified end-to-end in a production-equivalent environment. Specific scenarios listed in PR description. |
| **S5 — No sprint branch commits** | The sprint branch contains only merge commits from phase branches. No direct commits to the sprint branch. |
| **S6 — Architecture document current** | If the sprint introduced any entity, service, or interface not in the architecture document, the document is updated and committed. |
| **S7 — Known debt registered** | Any debt introduced during the sprint is recorded in the Known Debt section of the architecture document and in TASKS.md. |

---

## 8. Main Branch Protection Rules

`main` is always production-ready. The following are absolute:

- No direct commits to `main` — ever
- No force pushes to `main` — ever
- `main` is only updated via PR merge
- Every merge to `main` is a sprint completion or a hotfix
- `main` must build and all tests must pass at every commit

---

## 9. Rebase vs Merge Policy

| Situation | Action |
|-----------|--------|
| Phase branch → sprint branch | Merge (preserves phase history) |
| Sprint branch → main | Merge (preserves sprint history) |
| Hotfix → main | Merge |
| Sprint branch behind main (after hotfix) | Rebase sprint branch onto main |
| Phase branch behind sprint branch | Rebase phase branch onto sprint branch |
| Squashing WIP commits locally | Interactive rebase before push |

Never rebase a branch that has been pushed and shared. Rebase is local-only.

---

## 10. What Never Goes in a Commit

- Passwords, tokens, API keys, or connection strings — use user-secrets locally, environment variables in production
- `appsettings.Development.json` — excluded from publish and from commits
- Binary files other than images intentionally added to the project
- Compiled output (`bin/`, `obj/`)
- Commented-out code left "just in case" — Git is the history
- A migration without the feature that requires it
- A feature without its migration

---

## 11. Sprint Branch Sync Rule

The sprint branch must never be more than one hotfix behind `main`. If a hotfix merges to `main` while a sprint is in progress:

1. Sprint work pauses
2. Sprint branch rebases onto `main`
3. Active phase branch rebases onto updated sprint branch
4. Conflicts resolved
5. Sprint work resumes

This is non-negotiable. A sprint branch that diverges from main for more than one hotfix accumulates merge conflicts that compound with every subsequent commit.

---

## 12. Reference: V-Sprint 1 Branch Plan

```
main
└── vsprint-1
    ├── vsprint-1/phase-1-domain-infrastructure
    │     domain: SectionVersion entity + Create factory
    │     domain: ReadEvent.LastReadVersionNumber + UpdateLastReadVersion
    │     domain: Comment.SectionVersionId nullable FK
    │     domain: Project.ProjectType enum
    │     infra:  ISectionVersionRepository + EF implementation
    │     infra:  Generate AddVersioningAndManualUpload migration
    │     ── PR → vsprint-1 ──
    │
    ├── vsprint-1/phase-2-tree-service-import
    │     app:    SectionTreeService.GetOrCreateForUpload
    │     app:    SectionTreeService.GetTree
    │     app:    IImportProvider interface
    │     app:    RtfImportProvider implementation
    │     app:    ImportService.ImportAsync
    │     ── PR → vsprint-1 ──
    │
    ├── vsprint-1/phase-3-versioning-service
    │     app:    IVersioningService interface
    │     app:    VersioningService.RepublishChapterAsync
    │     ── PR → vsprint-1 ──
    │
    ├── vsprint-1/phase-4-reader-content-source
    │     web:    Reader views resolve from SectionVersion
    │     web:    Fallback to Section.HtmlContent (temporary)
    │     web:    ReadEvent.LastReadVersionNumber set on open
    │     web:    Comment.SectionVersionId set at creation
    │     ── PR → vsprint-1 ──
    │
    └── vsprint-1/phase-5-author-ui-manual-upload
          web:    Manual project creation flow
          web:    RepublishChapter controller action + button
          web:    UploadDraft controller action + file picker
          web:    AddSection controller action + modal
          ── PR → vsprint-1 ──

vsprint-1 ── PR → main  (after all phases + sprint gates satisfied)
```

---

## 13. Quick Reference Card

```
START SPRINT      git checkout main && git pull
                  git checkout -b vsprint-{N}
                  git push -u origin vsprint-{N}

START PHASE       git checkout vsprint-{N} && git pull
                  git checkout -b vsprint-{N}--phase-{P}-{slug}
                  git push -u origin vsprint-{N}--phase-{P}-{slug}

COMMIT            git add . && git commit -m "{type}: {summary}"

PHASE COMPLETE    Raise PR: phase branch → sprint branch
                  Checklist: G1–G8 satisfied
                  Merge

SPRINT COMPLETE   Raise PR: sprint branch → main
                  Checklist: S1–S7 satisfied
                  Merge

HOTFIX            git checkout main && git pull
                  git checkout -b hotfix/{slug}
                  [fix + test]
                  PR hotfix → main, merge
                  git checkout vsprint-{N} && git rebase main
                  git checkout vsprint-{N}--phase-{P}-{slug}
                  git rebase vsprint-{N}
```