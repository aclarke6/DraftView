# DraftView — Copilot Workspace Instructions

## Project Identity

DraftView is a beta reader platform for authors. Authors publish prose sections for review.
Beta readers comment on published content. The full architecture is in
`Publishing And Versioning Architecture.md`. The task list is in `TASKS.md`.

**Stack:** .NET 10, ASP.NET Core MVC, PostgreSQL, EF Core, ASP.NET Identity, xUnit/Moq  
**Solution:** `DraftView.slnx`  
**Layers:** Domain → Application → Infrastructure → Web  

---

## Layer Rules

**Domain** — entities, invariants, factory methods, domain services. No EF, no HTTP, no crypto,
no persistence concerns. Invariants enforced in factory methods or domain methods before any
caller can persist invalid state.

**Application** — orchestration services, interfaces, DTOs. Depends on domain and infrastructure
interfaces only. No EF DbContext. No controller concerns.

**Infrastructure** — EF Core, repositories, email, Dropbox, encryption. Implements application
interfaces. No domain logic.

**Web** — controllers, Razor views, ViewModels. Thin HTTP surface only. No business logic.
No repository calls. No domain mutations.

---

## TDD — Mandatory for Domain, Application, and Infrastructure

Every Domain, Application, and Infrastructure change follows this sequence:

1. Create the empty stub with `throw new NotImplementedException()`
2. Write failing tests that prove the requirement
3. Implement to make tests pass
4. Run the full test suite — zero regressions before proceeding
5. Refactor with tests green throughout

Never write production code before a failing test exists for it.
Never skip TDD because a change seems small.
Search existing tests before writing new ones — never duplicate a test.

---

## Controller Shape — Mandatory

Every controller action must follow this structure only:

1. Resolve current user via `RequireCurrentAuthorAsync()` or equivalent
2. Validate input — return early on failure
3. Call an application service
4. Map result to TempData or ViewModel
5. Return response

Controllers must never contain: loops over domain entities, repository calls, domain mutations,
branching business rules, or multi-step orchestration.

---

## Architecture — Two-Layer Ingestion Model

**Ingestion layer** (`ISyncProvider`, `IImportProvider`) writes to `Section.HtmlContent` only.  
**Platform layer** owns versioning, reader access, notifications, and publishing.

These rules are absolute:
- Sync never creates `SectionVersion` records — ever
- Import never creates `SectionVersion` records — ever
- Only `VersioningService.RepublishChapterAsync` (or `RepublishSectionAsync`) creates versions
- `SectionVersion.HtmlContent` is immutable after creation — no setter, no update path
- `SectionTreeService.GetOrCreateForUpload` is the only place a `Section` with `ScrivenerUuid = null` is created

---

## Entity Rules

- Every entity scoped to an author or reader resource carries `AuthorId`
- `AuthorId` is the tenancy anchor — it becomes `TenancyId` when multi-tenancy arrives
- Never add a global query that returns data without an author scope
- Soft-delete only — `IsSoftDeleted` + `SoftDeletedAt`. No physical deletes (except `SectionVersion` retention enforcement)
- Factory methods over public constructors for all domain entities

---

## Migration Rules

- Every schema change gets an EF Core migration immediately — never deferred
- Migration name describes the change: `AddSectionVersion`, `AddVersioningAndManualUpload`
- Migration is committed in the same batch as the feature code that requires it
- Never commit a migration without the feature; never commit a feature without its migration
- Review generated migrations — rewrite `DropTable/CreateTable` pairs as `RenameTable` where data must be preserved

---

## CSS Rules

- No inline `style=""` attributes in any Razor view — ever
- All styling via CSS classes in the appropriate existing stylesheet
- Every new CSS class has a comment indicating which view or component it belongs to
- No new stylesheet files without explicit agreement
- Bump `--css-version` in `DraftView.Core.css` on every CSS change (use `Update-CssVersion.ps1`)

---

## Naming Conventions

| Artefact | Pattern | Example |
|---|---|---|
| Domain entity | `PascalCase` noun | `SectionVersion` |
| Repository interface | `I{Entity}Repository` | `ISectionVersionRepository` |
| Application service interface | `I{Concern}Service` | `IVersioningService` |
| Application service | `{Concern}Service` | `VersioningService` |
| Import provider interface | `IImportProvider` | (single interface, multiple implementations) |
| Import provider implementation | `{Format}ImportProvider` | `RtfImportProvider` |
| Test class | `{SubjectClass}Tests` | `VersioningServiceTests` |
| Test method | `{Method}_{Condition}_{ExpectedOutcome}` | `RepublishChapterAsync_WithNoDocuments_ThrowsInvariantViolation` |

---

## What Copilot Must Never Do

- Write to `Section.HtmlContent` from any versioning service
- Create a `SectionVersion` from within `ISyncProvider` or `IImportProvider` implementations
- Put encryption, hashing, or HMAC logic in the domain layer
- Add `style=""` attributes to Razor views
- Call a repository directly from a controller
- Write production code without a failing test existing first
- Defer a migration to a later commit
- Use `&&` in PowerShell — use `;` instead
- Use `GetAllBetaReadersAsync()` or any unscoped reader query without an `AuthorId` parameter

---

## PowerShell

Full standards in `PowerShell.md`. Key rules:
- Never use `&&` — use `;`
- Every file-modifying script must read the file back and verify the change applied
- Detect line endings with `$le` before building match strings
- Scripts over 50 lines delivered as `.ps1` files, not pasted inline
- Every script starts with `cls` and `$ErrorActionPreference = "Stop"`

## Refactoring Rules
- See refactoring.md for comprehensive standards and examples
- 