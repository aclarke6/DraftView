---
mode: agent
description: BUG-020 — BetaBooksImporter dev tool broken by email encryption-at-rest migration
---

# BUG-020 — BetaBooksImporter dev tool broken by email encryption-at-rest migration

## Scope Note — Read First
This prompt covers **fixing the tool only**. It must be executed on the **Dev PC**,
against a local/dev database — never against production. Running the actual
BetaBooks import against the production database is a separate, later, explicitly
authorized step and is **out of scope** for this prompt. Do not connect to the
production connection string or the production server at any point while working
this task.

## Required Reading Before Starting
1. `AGENTS.md` — architecture boundaries, TDD rules, branching/commit rules (this
   task is governed by it in full)
2. `.github/Instructions/refactoring.instructions.md` — always required per AGENTS.md
3. `CLAUDE.md` — non-negotiable project rules (TDD, `dotnet test` after every
   red/green cycle, CSS version rules N/A here, PowerShell `param()` rule N/A here)
4. `TASKS.md` — current sprint/bug numbering context, to confirm BUG-020 is next

## Branching
1. Checkout `BugFix-PC` and pull latest from `main`
2. Create and checkout `bugfix/BUG-020-betabooks-importer-email-encryption` from `BugFix-PC`
3. All work is done on `bugfix/BUG-020-betabooks-importer-email-encryption`
4. When all Success Gates pass, present the merge commands to the developer — do not execute them
5. Developer merges: `bugfix/BUG-020-betabooks-importer-email-encryption` → `BugFix-PC` → `main`

## Symptoms
1. `DraftView.DevTools` `--import` mode (`BetaBooksImporter.RunAsync`, invoked from
   `Program.cs`) is a dev tool intended to import `betabooks-export.json` reader
   comments into a project. It has not been run since email encryption-at-rest was
   added and no longer functions.
2. `BetaBooksImporter.cs:37` and `:62` query `db.AppUsers.FirstOrDefaultAsync(u => u.Email == ...)`.
   `User.Email` (`DraftView.Domain/Entities/User.cs:10`) is `[NotMapped]` — the
   plaintext `Email` column was removed from `AppUsers` by migrations
   `AddProtectedEmailPersistenceFields` (2026-04-13) and
   `RemoveLegacyPlaintextUserEmail` (2026-04-14), replaced by `EmailCiphertext` /
   `EmailLookupHmac`. Any query against a `[NotMapped]` property throws
   `InvalidOperationException: The LINQ expression ... could not be translated` at
   runtime — the tool fails on its very first database call, before touching
   sections or comments.
3. Even with the query fixed, `BetaBooksImporter.RunAsync` constructs
   `DraftViewDbContext` via `new DraftViewDbContext(options)`
   (`BetaBooksImporter.cs:31-35`), which falls through to the **parameterless**
   `UserEmailEncryptionService()` / `UserEmailLookupHmacService()` constructors
   (`DraftViewDbContext.cs:22-25`). Both generate a **fresh random 32-byte key**
   via `RandomNumberGenerator.GetBytes()` on every construction
   (`UserEmailLookupHmacService.cs:13-16`, `UserEmailEncryptionService.cs` same
   pattern) instead of loading the real keys from configuration. Consequence:
   - a corrected HMAC-based lookup would still never match existing `AppUsers`
     rows, because the HMAC key differs from production/dev's real key
   - any reader created by the tool would have its email encrypted under a
     throwaway key that is discarded when the process exits, permanently
     undecryptable by the real application afterward
4. The tool also constructs `DraftViewDbContext` directly and issues raw LINQ
   against it from `DraftView.DevTools`, rather than going through
   `IUserRepository` / `ICommentRepository`. This conflicts with `AGENTS.md`
   (`No DbContext outside Infrastructure`, `All writes go through Application
   services`). The correct key-loading and lookup pattern already exists and is
   proven correct elsewhere:
   - `DraftView.Infrastructure/Persistence/Repositories/UserRepository.cs:18-20,39-41`
     — `u.EmailLookupHmac == ComputeLookupHmac(email)`
   - `DraftView.Infrastructure/Persistence/DraftViewDbContextFactory.cs:19-29` —
     loads `EmailProtection:EncryptionKey` / `EmailProtection:LookupHmacKey` from
     env var or user secrets and passes them into `UserEmailEncryptionService(key)`
     / `UserEmailLookupHmacService(key)`

## Where to Start Looking
Read the following in order. Do not write any code yet.

1. `DraftView.DevTools/BetaBooksImporter.cs` — the full broken import flow
2. `DraftView.DevTools/Program.cs` — how `--import` mode is invoked, what args it takes
3. `DraftView.Domain/Entities/User.cs` — `Email` is `[NotMapped]`; `EmailCiphertext`/`EmailLookupHmac` are the real persisted fields; `SetProtectedEmail` / `LoadEmailForRuntime`
4. `DraftView.Infrastructure/Persistence/Configurations/UserConfiguration.cs` — confirms only ciphertext/HMAC are mapped
5. `DraftView.Infrastructure/Persistence/Repositories/UserRepository.cs` — the correct lookup pattern (`GetByEmailAsync`, `EmailExistsAsync`, `ComputeLookupHmac`, `HydrateEmail`)
6. `DraftView.Infrastructure/Security/UserEmailEncryptionService.cs` and `UserEmailLookupHmacService.cs` — parameterless ctors generate random keys; keyed ctors are the real path
7. `DraftView.Infrastructure/Persistence/DraftViewDbContextFactory.cs` — how the real keys are located and loaded at design time (same problem the importer needs solved, but for a console tool at run time)
8. `DraftView.Infrastructure/Persistence/DraftViewDbContext.cs` — `PrepareProtectedEmails()` auto-encrypts a `User.Email` set on an `Added`/`Modified` entity at `SaveChangesAsync` time, given the context was built with real keys
9. `DraftView.Web/Extensions/ServiceCollectionExtensions.cs` — how the real app registers `IUserEmailEncryptionService` / `IUserEmailLookupHmacService` with real keys via DI, as the reference for what "correct" wiring looks like
10. `betabooks-export.json` (repo root) — the data shape being imported (`book`, `exported_from`, `comments[]` with `chapter`, `reader`, `posted_at`, `body`, `author_reply`)
11. `DraftView.Domain/Entities/Comment.cs` — `CreateForImport` factory (already correct, no schema drift observed here)

## What to Produce — Plan First, Then Pause
After reading all files, produce a written plan containing all four sections below.
Stop after the plan. Do not write any code. Wait for the plan to be reviewed and
approved by the developer.

### Section 1 — Root Cause Analysis
State precisely:
- Why the tool currently throws before doing any useful work
- Why fixing only the query (HMAC comparison) is not sufficient on its own
- Whether the fix should stay a standalone console tool with its own key-loading
  (mirroring `DraftViewDbContextFactory`), or whether it should be refactored to
  go through `IUserRepository`/`ICommentRepository` via the app's real DI
  container (preferred per `AGENTS.md` — "No DbContext outside Infrastructure",
  "All writes go through Application services") — recommend one and justify it
- Confirm whether `Comment.CreateForImport` and the section-title matching logic
  are still valid against current schema (no drift expected, but verify)

### Section 2 — Failing Test Plan
There is currently no `DraftView.DevTools` test project. Decide and state:
- Whether to add tests at the `DraftView.Infrastructure.Tests` level (using
  `UseInMemoryDatabase`, per `ScrivenerProjectRepositoryTests` pattern) that
  exercise the corrected lookup/key-loading logic directly, or to create a new
  `DraftView.DevTools.Tests` project — pick one, do not do both without reason
- For each test: test class and method name, what it seeds/arranges, what it
  calls, what assertion it makes, why it currently fails (red), why it passes
  after the fix (green)

Tests must cover at minimum:
- A user lookup by email succeeds when the context is built with a known
  encryption/HMAC key pair and a user was seeded/encrypted with that same key
  pair (proves the query is now translatable and correct)
- Two separately constructed contexts using the *same* configured key produce
  the *same* `EmailLookupHmac` for the same email (proves keys are no longer
  randomly regenerated per run)
- A reader created via the import path is later findable by the same lookup
  used to check for an existing reader (idempotency guard for reruns), OR — if
  idempotency is out of scope — an explicit note in the plan that reruns will
  create duplicate readers/comments and this is accepted as-is
- No regression to `Comment.CreateForImport` behavior or chapter/section matching

### Section 3 — Proposed Fix
Describe the fix in plain English before touching any code. State:
- Which file(s) change and why
- Exactly how the tool will obtain the real `EmailProtection:EncryptionKey` /
  `EmailProtection:LookupHmacKey` at run time (env var, user secrets, or a new
  `--connection`/`--keys` style argument — pick one, consistent with how
  `Program.cs` already takes `connString` as an argument)
- Whether `DraftView.DevTools/DraftView.DevTools.csproj` needs a new project
  reference (e.g. to reuse `DraftView.Application`'s service registration) if the
  DI/repository approach is chosen
- What is explicitly not changing (e.g. `Comment.CreateForImport`, JSON shape,
  chapter-matching heuristic)
- Confirm no migration is required (this is a bug fix to a console tool, not a
  schema change)

### Section 4 — Success Gates

**Gate 1 — New tests are red before the fix**
- [ ] All new tests confirmed red — paste failing output

**Gate 2 — New tests are green after the fix**
- [ ] All new tests pass — paste passing output

**Gate 3 — No regressions**
- [ ] Full suite run — paste count: X passing, 0 failed, N skipped

**Gate 4 — Local dry run (Dev PC, local/dev database only — never production)**
- [ ] Run `DraftView.DevTools --import <local-dev-connection-string> betabooks-export.json <dev author email>` against a local/dev database seeded with a project named `Book 1 - The Fractured Lattice` and matching chapter-titled section folders
- [ ] Confirm the author is found
- [ ] Confirm all 34 comments in `betabooks-export.json` are processed (imported or explicitly accounted for as skipped, with reasons printed)
- [ ] Confirm reader accounts created by the tool are subsequently findable via `IUserRepository.GetByEmailAsync` using the same keys — i.e., not orphaned by a mismatched key
- [ ] Confirm no plaintext email or key material is logged to console output

**Gate 5 — Committed to GitHub**
- [ ] Committed to `bugfix/BUG-020-betabooks-importer-email-encryption` with message:
      `bugfix: BUG-020 — fix BetaBooksImporter email lookup and key loading after encryption-at-rest migration`
- [ ] `git status` is clean

**Gate 6 — TASKS.md updated**
- [ ] `TASKS.md` updated to mark BUG-020 as `[DONE]` with date and resolution summary, in the same style/section as BUG-017/BUG-018
- [ ] Included in same commit batch

**Gate 7 — Present merge commands**
- [ ] Present for manual execution — do not execute:
      `git checkout BugFix-PC && git merge bugfix/BUG-020-betabooks-importer-email-encryption`
      (and the subsequent `BugFix-PC` → `main` merge), for the developer to run manually

## Rules
- Do not change any production code until the plan has been reviewed and approved by the developer
- TDD: failing test → confirm red → fix → confirm green, per `AGENTS.md` and `CLAUDE.md`
- Never connect to the production database or production server during this task — local/dev database only
- Never log decrypted emails, ciphertext, or key material to console output in the fixed tool
- Existing tests must not be modified to make new tests pass
- All git commands presented to the developer for manual execution — never executed automatically
- A task is not complete until every Success Gate is confirmed
- The actual production BetaBooks import run is a separate follow-up task, not part of this one
