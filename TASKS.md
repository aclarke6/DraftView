# DraftView Task List
Last updated: 2026-04-14

---

## ARCHITECTURE RULES

These rules govern all new development and must be applied consistently.

### Tenancy Stance: Tenancy-Agnostic
Build everything tenancy-agnostic — not tenancy-unaware, not tenancy-enabled.

**The rule:** If a table relates to a reader, project, or author-scoped resource, it must carry an `AuthorId`. When full multi-tenancy arrives, `AuthorId` becomes `TenancyId` and the queries stay the same shape. The refactor is then mechanical (migration + rename), not architectural.

**Checklist for every new entity:**
- Does it relate to a reader, project, or author-specific resource? → Add `AuthorId`
- Does the repository method return scoped data? → It must accept or imply an `AuthorId`
- Is there a global query with no author scope? → That is a bug, not a feature

**What this means in practice:**
- `ReaderTenant` gets `AuthorId` now, renamed to `TenancyId` later
- Invitation flow must carry `AuthorId` explicitly
- Every Readers list query must be scoped to the current author
- Never use `GetAllBetaReadersAsync()` without an author scope (current usage is a known debt)

### No Tenancy-Enabled Premature Build
Do not build the full `Tenancy` / `TenancyMembership` entity model until billing is in place and the product is live with a single author. Reader Marketplace is explicitly deferred until then.

### TDD Required for Domain, Application and Infrastructure Changes
All new domain entities, application service changes, and infrastructure changes require tests before implementation. No exceptions.

### CSS Version — MANDATORY on Every CSS Change
Every script that modifies any `.css` file must also bump `--css-version` in `DraftView.Core.css`. Automated via `Update-CssVersion.ps1`.

Always use regex replace — never hardcode the expected current value:
    $core = $core -replace '--css-version: "v[^"]+"', '--css-version: "vNEW_VERSION"'

### Controller Action Guards — MANDATORY
Every controller that accesses data or performs mutations must be protected by an `[Authorize]` attribute at class level:

- `AuthorController`  → `[Authorize(Policy = "RequireAuthorPolicy")]`
- `ReaderController`  → `[Authorize(Roles = "Author,BetaReader")]` (via `BaseReaderController`)
- `SupportController` → `[Authorize(Roles = "SystemSupport")]`
- `DropboxController` → `[Authorize(Policy = "RequireAuthorPolicy")]`
- `AccountController` → individual actions use `[Authorize]` where needed

### Replacement Scripts Must Verify — MANDATORY
Every PowerShell string replacement MUST verify the change applied before proceeding:
1. Detect line endings: `$le = if ($content -match "\`r\`n") { "\`r\`n" } else { "\`n" }`
2. Apply replacement using `$le` in match strings
3. Compare old and new — if equal, write ERROR and exit 1
4. Only then proceed

### Script Standards
- Name format: `Step{N}-{DayAbbrev}-{Description}.ps1`
- Start with `cls`; end with the next required command
- Unicode characters built via `[char]0xNNNN` — never embed in here-strings
- Scripts 50+ lines → deliver as `.ps1` files via `present_files`
- Short blocks (<50 lines) pasted directly — must still include `cls`, `$le`, and verification

### Full File Rewrites Over Regex Patching
For complex files, prefer full rewrites over inline regex patching.

### View Style Leakage Audit — MANDATORY
Any modification of a view must include an audit of that view for style leakage.

- Any style leakage must be reported
- If appropriate, leaked inline or view-local styling must be replaced with CSS
- A `[DONE]` update reporting the style fix must be added under `## DONE (this project)` in `### Additional Completed Tasks`

---

## BUGS

none currently logged — add here as discovered

---

## Test state

- 449 Tests total
- One skipped test is `SmtpEmailSenderIntegrationTests` which sends a real email, so is not suitable for regular test runs but is included in the solution for manual execution when needed.
- Latest full passing count: 449 total, 448 passed, 1 skipped, 0 failed
- Latest targeted application count: 128 total, 128 passed, 0 skipped, 0 failed

---

## Sprint 4 — Email Privacy and Controlled Access (PHASED EXECUTION)

Ensure user email addresses are protected, not exposed to other users, and only accessible through controlled, auditable mechanisms. Align system behaviour with UK GDPR principles of data minimisation, access control, and protection by design.

Email handling model:
- Email stored in encrypted form
- Email lookup via deterministic HMAC of normalised email
- Encryption and decryption are performed through application/infrastructure services, not in the domain model
- Email never exposed in UI beyond explicitly permitted views
- Controlled access for administrative purposes only
- New views fail closed by default unless explicitly whitelisted

---

## Phase 1 [DONE] — Rules and Governing Red Tests

- [DONE] Defined the whitelist for stored-email rendering, restricted to `Views/Account/Settings.cshtml`
- [DONE] Added governing source-level and rendered-output email-exposure regressions for non-whitelisted pages
- [DONE] Seeded initial red-state evidence for `AcceptInvitation`, `ManageReaderAccess`, `Readers`, and shared layout leakage
- [DONE] Added a behavioural `/Account/Login` web regression to preserve login flow during later email-model changes
- [DONE] Confirmed sprint start state: email-exposure governing tests were RED and behavioural guard tests were expected GREEN

---

## Phase 2 [DONE] — Web Surface Cleanup (Fast Feedback)

- [DONE] Removed non-whitelisted stored-email display from `AcceptInvitation`, `ManageReaderAccess`, `Readers`, and shared layout navigation
- [DONE] Stopped `AccountController.AcceptInvitation` GET from passing stored email into a non-whitelisted rendered view
- [DONE] Confirmed other reviewed non-whitelisted controller/view paths no longer pass stored email for display
- [DONE] Made the source-level and rendered-output email-exposure governing tests GREEN
- [DONE] Kept the `/Account/Login` behavioural regression GREEN throughout cleanup

---

## Phase 3 — Infrastructure (Data Shape First)

**Goal:** Establish secure persistence model

**Contract and Tests**
- [DONE] Defined the protected email persistence contract for infrastructure
  - `AppUsers.Email` plaintext persistence was replaced by `EmailCiphertext` and `EmailLookupHmac`
  - plaintext email is not written to `AppUsers`
  - lookup uses deterministic HMAC of normalised email for equality checks and uniqueness
  - encryption and decryption remain outside the domain model
- [DONE] Added the governing infrastructure tests for protected email persistence
  - encryption does not store plaintext
  - decryption restores the original value
  - HMAC lookup is deterministic
  - different inputs produce different lookup values
  - no plaintext email is persisted for new or updated users

**Schema and Migration**
- [DONE] Added the protected email schema fields
  - `EmailCiphertext`
  - `EmailLookupHmac`
- [DONE] Added and validated the unique index on `EmailLookupHmac`
- [DONE] Applied the protected email migration
- [DONE] Fixed the populated-database migration bug so uniqueness is enforced safely on existing data

**Encryption and HMAC Seams**
- [DONE] Introduced the application/infrastructure seam for email encryption and decryption
  - application owns the contract
  - infrastructure owns the implementation
  - domain owns no encryption mechanics
- [DONE] Introduced the application/infrastructure seam for deterministic email lookup HMAC generation
  - application owns the contract
  - infrastructure owns the implementation
  - domain owns no HMAC mechanics
- [DONE] Tightened the domain-boundary tests so they detect crypto implementation concerns rather than protected-field naming alone

**Persistence and Lookup Wiring**
- [DONE] Removed EF mapping of plaintext `User.Email`
- [DONE] Added save-time protection to generate `EmailCiphertext` and `EmailLookupHmac`
- [DONE] Rehydrated runtime `User.Email` from ciphertext on repository reads
- [DONE] Replaced repository lookups and existence checks to use protected lookup rather than plaintext email queries
- [DONE] Patched direct `DbContext` email queries in startup and seeding paths to use protected lookup and runtime rehydration

**Compatibility and Refactor**
- [DONE] Restored full-suite compatibility after the `DraftViewDbContext` constructor change
  - `DraftView.Web.Tests.Controllers.AccountControllerTests` compatibility was restored without undoing protected-email persistence behaviour
- [DONE] Refactored Phase 3 as one coherent unit without widening into Phase 4
  - extracted helpers from `DraftViewDbContext`
  - removed duplication in the normalize -> HMAC -> encrypt -> protect flow
  - simplified repository hydration helpers
  - extracted small local helpers from `DatabaseSeeder`
  - removed low-value transitional constructor fallback plumbing
- [DONE] Re-audited the Phase 3 architecture boundaries
  - infrastructure owns persistence, encryption, HMAC, save-time protection, and protected lookup
  - domain owns no crypto or persistence mechanics
  - application contracts remain small and explicit

**Verification**
- [DONE] `dotnet test DraftView.Infrastructure.Tests --nologo` returned GREEN: 96 passed, 0 failed
- [DONE] `dotnet test --nologo` returned GREEN: 449 total, 448 passed, 1 skipped, 0 failed

---

## Phase 4 — Application Layer (Behaviour and Access Control)

**Goal:** Control access and orchestrate protection

**Services**
- [DONE] Introduce `IUserEmailProtectionService`
- [DONE] Introduce `IUserEmailAccessService`
- [DONE] Introduce explicit authentication lookup seam for resolving a user from login email input via protected lookup

**Access Rules**
- [ ] Self access permitted
- [ ] `SystemSupport` access permitted for explicit administrative/support purposes
- [ ] Author access to reader stored email denied once an invitation has been sent
- [ ] All other access denied (deny-by-default)

**Orchestration**
- [ ] Authorisation check occurs before decryption
- [ ] Decrypt only after approval
- [ ] Centralise all email access through service
- [ ] Route login identity resolution through the protected authentication lookup seam rather than plaintext email lookup
- [ ] Migrate `AccountController.Settings` self-access email display to the Phase 4 application seam
- [ ] Migrate broader current-user resolution paths away from controller-level `User.Identity?.Name` plus generic repository email lookup
- [ ] Add a `SystemSupport`-only privileged email access path only if an explicit support consumer is required

**Application TDD**
- [ ] Write failing tests:
  - self access allowed
  - unauthorised access denied
  - authorised access allowed
  - decrypt not called when access denied
- [ ] Create regression test: email access is denied by default unless authorised
- [DONE] Write failing lower-level integration/authentication regression test:
  - authentication lookup resolves the correct user from login email input via protected lookup
  - do not bind this test to `IUserRepository.GetByEmailAsync`
- [ ] Implement to green
- [ ] Refactor

**High-level stages**
- [DONE] Stage 1: Define the application seams for protected login lookup and controlled email access
- [ ] Stage 2: Add governing application tests for deny-by-default access and no-decrypt-before-authorisation
- [ ] Stage 3: Route self-service and current-user email access through the application layer
    - [ ] Stage 3 should include:
        - `AccountController.Settings` self-access email display
  - broader current-user application access patterns that currently depend on controller-level identity email lookup
- [ ] Stage 4: Route privileged admin/support email access through the application layer with explicit authorisation
    - [ ] Stage 4 should include:
        - any explicit support/admin email access consumer that is required (avoid building this if no consumer is needed)
- [ ] Stage 5: Route authentication identity resolution through the protected lookup seam instead of direct email lookup assumptions
    - [ ] Stage 5 should include:
        - new authentication lookup seam for resolving a user from login email input
        - migration of the login flow to use the new seam rather than direct repository lookup by email
- [ ] Stage 6: Refactor Phase 4 as one unit and verify the full suite remains green

**Stage 1 breakdown**
- [DONE] Stage 1.1: Define the application contract for controlled email access
  - introduce `IUserEmailAccessService`
  - service decides whether a caller may access a target user's email
  - deny-by-default is the contract baseline
  - `SystemSupport` is the only privileged cross-user access role in this phase
  - `Author` does not gain stored-email access to readers once an invitation has been sent
- [DONE] Stage 1.2: Define the application contract for protected email material resolution
  - introduce `IUserEmailProtectionService`
  - service orchestrates decrypting protected email only after approval
  - service depends on the existing infrastructure encryption seam rather than duplicating crypto logic
  - Stage 1.2.1: define the responsibility of `IUserEmailProtectionService`
    - service resolves a user's stored email only after access has been approved
    - service is orchestration, not policy and not crypto implementation
  - Stage 1.2.2: define the minimum input required
    - target user id
    - approved-access context or explicit pre-authorised request shape
    - avoid raw email as the lookup key for this service
  - Stage 1.2.3: define the output shape
    - resolved plaintext email only when access is already authorised
    - failure path should be safe and explicit
    - no silent fallback to plaintext persistence paths
  - Stage 1.2.4: define service composition with `IUserEmailAccessService`
    - `IUserEmailAccessService` decides whether access is allowed
    - `IUserEmailProtectionService` decrypts only after approval
    - keep policy and material resolution separate
  - Stage 1.2.5: define dependency boundaries
    - application service may depend on `IUserRepository`
    - application service may depend on existing `IUserEmailEncryptionService`
    - application service must not implement encryption itself
    - application service must not depend on plaintext database queries
  - Stage 1.2.6: define failure behaviour
    - missing target user fails safely
    - missing ciphertext or invalid ciphertext fails safely
    - denied access must return without attempting decryption
  - Stage 1.2.7: identify first consumers for later migration
    - account settings self-email display
    - any future support-only email reveal path
    - keep login lookup out of this service because that belongs to the authentication lookup seam
  - Stage 1.2.8: add interface and compile-only placeholder wiring if needed
    - no controller migration yet
    - no behaviour change yet
    - no Phase 5 domain changes yet
- [DONE] Stage 1.3: Define the application contract for authentication lookup via protected email input
  - introduce an explicit authentication lookup seam for resolving a domain user from login email input
  - contract should be phrased around authentication/login lookup, not generic repository email lookup
  - seam should be named around login/authentication intent rather than generic email lookup
  - input is login email input and output is the matching domain user or null
  - normalization and protected lookup remain internal to the seam
  - do not bind Phase 4 tests to `IUserRepository.GetByEmailAsync`
- [DONE] Stage 1.4: Define the request and response shapes for controlled access
  - identify required inputs:
    - requesting user id or authenticated context
    - target user id
    - access purpose where relevant
  - identify required outputs:
    - allowed/denied result
    - resolved email only when authorised
  - use an explicit request/result pair for `IUserEmailAccessService`
  - keep plaintext email out of the access-decision result
  - use target user id rather than raw email as the controlled-access identifier
  - use an enum for access purpose rather than free text
  - keep the authentication lookup seam separate from the controlled-access request/response model
- [DONE] Stage 1.5: Define the authorisation ownership boundary
  - application layer owns access decisions and orchestration
  - infrastructure layer continues to own encryption, decryption, HMAC generation, and protected persistence
  - domain layer continues to own no crypto mechanics
  - application layer owns the authentication-oriented lookup contracts and controlled-access request/result models
  - controllers must not own email-disclosure policy or decryption logic
  - infrastructure must not own `SystemSupport` versus self-access policy decisions
- [DONE] Stage 1.6: Identify and list the first consumers to migrate in later stages
  - self-service settings/current-user flows
  - admin/support privileged access flows
  - login identity resolution flow
  - avoid widening Stage 1 into implementation of those consumers
  - migrate in this order:
    - account settings self-access email display
    - current-user application access pattern
    - support-only privileged access path
    - login/authentication lookup seam adoption
- [DONE] Stage 1.7: Add compile-only wiring or stubs only if needed to unblock Stage 2 tests
  - no behaviour change yet
  - no controller migration yet
  - no Phase 5 domain changes yet
  - if needed, add interfaces and minimal request/result types only
  - if needed, add `NotImplementedException` or equivalent compile-only placeholder implementations
  - do not change existing controller/service consumers in this stage
  - keep Stage 1 limited to design and compilation readiness for Stage 2 tests
  - compile verification GREEN: `dotnet build --nologo`
  - full-suite verification GREEN: 449 total, 448 passed, 1 skipped, 0 failed

**Stage 2 breakdown**
- [DONE] Stage 2.1: Add failing tests for `IUserEmailAccessService`
  - self access allowed
  - `SystemSupport` access allowed for explicit support/admin purpose
  - author access to reader stored email denied
  - all other cross-user access denied by default
- [DONE] Stage 2.1 implementation to green
  - `dotnet test DraftView.Application.Tests --nologo` returned GREEN: 121 passed, 0 failed
- [DONE] Stage 2.2: Add failing tests for `IUserEmailProtectionService`
  - authorised access can resolve stored email
  - denied access does not attempt decryption
  - missing target user fails safely
  - invalid or missing ciphertext fails safely
- [DONE] Stage 2.2 implementation to green
  - `dotnet test DraftView.Application.Tests --nologo` returned GREEN: 124 passed, 0 failed
- [DONE] Stage 2.3: Add failing tests for the composition boundary between access and decryption
  - access decision occurs before decryption
  - decryption service is not called when access is denied
  - plaintext email is not surfaced through fallback behaviour
- [DONE] Stage 2.3 implementation to green
  - `dotnet test DraftView.Application.Tests --nologo` returned GREEN: 126 passed, 0 failed
- [DONE] Stage 2.4: Add failing tests for the authentication lookup seam
  - login email input resolves the correct domain user
  - login lookup uses the protected path conceptually
  - tests are not written against `IUserRepository.GetByEmailAsync` as the contract under test
- [DONE] Stage 2.4 implementation to green
  - `dotnet test DraftView.Application.Tests --nologo` returned GREEN: 121 passed, 0 failed
- [DONE] Stage 2.5: Add a deny-by-default regression test
  - if a case is not explicitly allowed, access is denied
  - protects against later accidental widening of privileged access
- [DONE] Stage 2.5 verification
  - `dotnet test DraftView.Application.Tests --nologo` returned GREEN: 128 passed, 0 failed
- [DONE] Stage 2.6: Add compile/test scaffolding only where needed
  - mocks and fixtures for requester, target user, repository, and encryption service
  - no controller migration yet
  - no implementation beyond what tests require
- [DONE] Stage 2.6 review
  - required scaffolding was added incrementally across the Stage `2.1` to `2.5` application tests
- [ ] Stage 2.7: Run targeted application tests and keep `TASKS.md` current
  - red first
  - then implement in later Stage 2/3 work until green

---

## Phase 5 — Domain Refinement

**Goal:** Enforce correct domain model without leaking concerns

**Domain Changes**
- [ ] Remove public `Email` property
- [ ] Introduce:
  - `EmailCiphertext`
  - `EmailLookupHmac`
- [ ] Add method to set protected email state:
  - validate format
  - normalise email
  - accept only protected values

**Domain TDD**
- [ ] Write failing tests:
  - valid email accepted
  - invalid email rejected
  - normalisation applied consistently
  - no plaintext persistence
- [ ] Implement to green
- [ ] Refactor

---

## Phase 6 — End-to-End Integration

**Goal:** Ensure system flows work with new model

**Flows**
- [ ] Registration:
  - normalise email
  - compute HMAC
  - encrypt before save
- [ ] Invitation:
  - same protection applied
- [ ] Login:
  - normalise input
  - compute HMAC
  - lookup via `EmailLookupHmac`

**Integration Tests**
- [ ] Login works via HMAC lookup
- [ ] Registration persists protected values only
- [ ] No plaintext email stored
- [ ] Governing tests move towards GREEN

---

## Phase 7 — Audit and Security Hardening

**Audit Logging**
- [ ] Log all privileged access attempts:
  - requesting user ID
  - target user ID
  - timestamp
  - success or failure
  - reason if provided
- [ ] Ensure logs do NOT include plaintext email

**Access Control**
- [ ] Enforce explicit permission for admin/support access
- [ ] Ensure least privilege across system

**Security Tests**
- [ ] Verify:
  - email not exposed in unauthorised views
  - logs contain no sensitive data
  - access rules enforced correctly

---

## Phase 8 — Publish and Live Key Management

**Goal:** Deploy protected email handling safely to Linux/live environments

- [ ] Verify compatibility with the real `Publish-draftview.ps1` workflow
  - publish requires a clean git state before deployment
  - publish copies fresh output to `/var/www/draftview`
  - publish restarts the `draftview` systemd service
- [ ] Define how the live Linux environment provides persistent encryption key material
- [ ] Ensure live key material is stored outside the replaced publish payload where necessary
- [ ] Ensure key material persists across app restarts and deployments
- [ ] Separate local/test key material from live key material
- [ ] Document the publish-time secret/key setup process
- [ ] Define operational guidance for key rotation and recovery
- [ ] Verify post-publish behaviour:
  - login still works
  - protected email lookup still works
  - existing encrypted data remains decryptable after `Publish-draftview.ps1` completes and the service restarts

---

## UAT
- [ ] User can register and login using email
- [ ] Email not visible to other users
- [ ] Authors cannot see beta reader email post-invite
- [ ] User can view and update own email in settings
- [ ] Admin/support access is restricted and logged
- [ ] No regressions in authentication, invitation, or notification flows

---

## Compliance
- [ ] Privacy notice updated and visible
- [ ] Email usage limited to service-related communication
- [ ] No marketing usage implemented
- [ ] No third-party sharing for unrelated purposes
- [ ] Data handling aligns with UK GDPR and Data Protection Act 2018

---

## Definition of Done
- [ ] Governing regression tests created and verified RED at sprint start
- [ ] Governing tests fully GREEN at sprint completion
- [ ] All Domain, Application, and Infrastructure changes developed via TDD
- [ ] Review Sprint 4 infrastructure tests and decide which remain as permanent regression coverage versus which should be rewritten or removed as transitional implementation-lock tests
- [ ] Full test suite passing
- [ ] Green test count reported
- [ ] No plaintext email stored in database
- [ ] No email exposure in UI beyond whitelist
- [ ] Audit logging verified
- [ ] Migrations applied and validated
- [ ] Manual browser verification complete (desktop and mobile)
- [ ] Changes committed with clear message
- [ ] TASKS.md updated

## SPRINT 5 — Kindle-style Resume — Exact Scroll Position

Current state: resume redirects to correct scene via `#scene-{id}` anchor but does not restore exact scroll position within the scene. `ReadEvent` has no `ScrollPosition` field.

**Domain (TDD required)**
- [ ] Add `ScrollPosition` (nullable int) to `ReadEvent` entity
- [ ] Add `UpdateScrollPosition(int pixels)` domain method to `ReadEvent`
- [ ] Domain tests: `UpdateScrollPosition_SetsScrollPosition`, `UpdateScrollPosition_OverwritesPreviousValue`

**Infrastructure**
- [ ] EF Core migration: add `ScrollPosition` column to `ReadEvents` (nullable int)

**Application (TDD required)**
- [ ] Add `UpdateScrollPositionAsync(Guid sectionId, Guid userId, int scrollPosition)` to `IReadingProgressService`
- [ ] Failing tests → implement: find existing `ReadEvent`, call `UpdateScrollPosition`, save
- [ ] Update `GetLastReadEventAsync` tests to cover `ScrollPosition` being returned

**Web — Controller**
- [ ] `[HttpPost] RecordScrollPosition(Guid sectionId, int scrollPosition)` on `ReaderController` — returns `Ok()`
- [ ] Resume redirect in `DesktopDashboard` / `MobileDashboard` — use `?scrollTo={ScrollPosition}` if stored, fall back to `#scene-` anchor if null

**Web — JavaScript (DesktopRead.cshtml)**
- [ ] On scroll (debounced 500ms), POST `window.scrollY` to `RecordScrollPosition` for the currently visible scene
- [ ] On page load, if `?scrollTo=` query param present, `window.scrollTo(0, scrollTo)` after short delay

**UAT**
- [ ] Read scene partway through, sign out, sign in — confirm returned to exact scroll position
- [ ] Works across multiple projects (most recent across all)
- [ ] Graceful fallback if section no longer published

---

## SPRINT 6 — Platform Hardening

- [ ] Fail2ban setup on production VM
- [ ] Report Fault modal — HomeController POST + _Layout.cshtml modal + CSS
- [ ] SystemStateMessage expiry — add `ExpiresAt` nullable DateTime, `GetActiveAsync` filters expired
- [ ] Add Project discovery flow (`IScrivenerProjectDiscoveryService` + Projects page UI)
- [ ] Logging: failed authorization attempts (Role Migration carry-over)
- [ ] Impersonation — read-only, explicit enter/exit mode (design agreed, not yet built)

---

## SPRINT 7 — Billing & Multi-tenancy (Post Go-Live)

- [ ] IBillingProvider abstraction (Creem preferred, Paddle alternative)
- [ ] Subscription tiers: Free / Paid / Ultimate
- [ ] ReaderTenant model (AuthorId, IsActive, IsDeleted, KnownAs)
- [ ] Account / TenancyMembership model (per v3 business model doc)
- [ ] Mark intentional single-tenancy seams for future refactor
- [ ] Reader Marketplace — reader discoverable to other authors (ReaderProfile, GenreList)
- [ ] Standalone Sync Worker — extract SyncBackgroundService into separate worker project

---

## GO-LIVE GATE

Completed on the day of go-live, not before:
- [ ] Send password reset emails to Becca (becca@the-dunlops.co.uk) and Hilary (hilaryrrb@gmail.com)
- [ ] Confirm Becca and Hilary can log in and access The Fractured Lattice

# DraftView Publishing and Versioning Architecture (v3.0)
See Publishing And versioning Architecture.md for the full architecture document including Sprints 1-7
- Build versioning in stages, starting with a simple Republish → Version → Reader flow
- Add paragraph diff highlighting early to deliver core user value
- Layer in reader UX, then author-side change indicators and AI summaries
- Introduce advanced features later: scene publishing, scheduling, locking, and sync
- Keep each sprint small, complete, and testable—no mixing scope or partial features

---

## BACKLOG — Go-Live Requirements

### Reader UX
- Reader notification emails (new chapter published)

### Author Dashboard
- Show last download timestamp alongside last sync timestamp

### Dropbox
- Dropbox OAuth2 token refresh — automatic refresh using stored refresh token
- Dropbox webhook controller for push-based sync (replace polling)
- Incremental sync — only download changed files (cursor-based)
- In-app Dropbox re-auth page

### Author Views — Mobile
- Readers page mobile: name, status, Deactivate only
- Author/Comments view: per chapter, all scene comments, reply/delete inline
- Recent Activity: tap to open Author/Comments for that scene

### Author Chapter Page (Author/Chapter/{id})
- Full chapter view with all scenes
- Scene comments on RHS sidebar
- Chapter level comments in floating bar at bottom
- Reader progress: who has read it, date last visited
- Link from Sections list

### Author Scene Page
- Link back to parent chapter page
- Reader count hover showing which readers have read this scene

### Publishing Cascades
- Part-level publish cascades all chapters + scenes below
- Book-level publish cascades everything below

### Config
- Audit remaining user secrets — anything not a password/token/key belongs in appsettings

---

## BACKLOG — Architecture (Phase 1-5, pre-tenancy)

Work captured for future sprints. Do not start until the relevant sprint is active.

### Phase 1 — Stabilise single-tenancy
- Pass CancellationToken consistently in SyncService
- Validate section existence in ReadingProgressService.RecordOpenAsync
- Define and enforce active/inactive/soft-deleted user rules

### Phase 2 — Move mutations out of controllers
- ActivateProject, DeactivateProject, RemoveProject, AddProjects → application services
- Remove GetUnitOfWork(), GetCommentService(), GetReadEventRepo() service location
- Replace with constructor injection

### Phase 3 — Tighten application workflows
- Publication flow: enforce authorId or remove it
- Dashboard queries: move UI-shaped queries out of repositories

### Phase 4 — Make sync safer
- Replace controller Task.Run sync kickoff
- Check moved/reappearing soft-deleted section handling in reconciliation
- Add operational visibility: log discovery/parse failures

### Phase 5 — Prepare tenancy move
- Document single-tenancy seams: User.Role, GetAuthorAsync, GetAllBetaReadersAsync,
  GetReaderActiveProjectAsync, user preferences scoping, comment visibility model

---

## BACKLOG — CSS / Frontend

- [ ] CSS naming conventions refactor (BEM consistency)
- [ ] Remove duplicate `.comment-box__reply-form` declaration in Reader.css
- [ ] Replace hardcoded `#f8f8f6` in `.chapter-comment-form` with `var(--color-surface-alt)`
- [ ] Replace hardcoded `15px` in `.chapter-comment-form__textarea` with `var(--text-base)`

---

## DONE (this project)

### Additional Completed Tasks

- [DONE] Layout top bar now shows the current user display name instead of email; falls back to `Account settings` when display name is missing, with hover text `Account settings`
- [DONE] Fixed style leakage by scoping prose font preferences to reader surfaces only so system UI remains on standard typography

## Sprint 3 — Reader Font Preferences (IMPLEMENTED)

Allow users (Authors + Beta Readers) to choose their preferred reading font and size, persisted per user, applied globally via layout and CSS variables.

Font faces:
- SystemSerif (default)
- Humanist (Merriweather)
- Classic (Times)
- SansSerif (Lato)

Font sizes:
- Small
- Medium (default)
- Large
- ExtraLarge

**Domain**
- [DONE] Added `ProseFont` and `ProseFontSize` to `UserPreferences`

**Application**
- [DONE] Preferences loaded via `UserPreferencesRepository`
- [DONE] Preferences applied in `_Layout.cshtml`

**Web — Layout**
- [DONE] Injected `data-prose-font` and `data-prose-font-size` onto `<body>`
- [DONE] Fonts loaded via Google Fonts (Merriweather, Lato)
- [DONE] No inline styling — CSS driven

**Web — CSS (Core)**
- [DONE] `--font-prose` controlled via `data-prose-font`
- [DONE] `--text-prose-base` controlled via `data-prose-font-size`
- [DONE] Explicit mappings for all font options (including SystemSerif)
- [DONE] Size scale adjusted for meaningful visual separation

**Web — Reader Views**
- [DONE] DesktopReader uses `var(--font-prose)` and `var(--text-prose-base)`
- [DONE] MobileReader uses same variables
- [DONE] No duplication of logic between views

**Web — Settings UI**
- [DONE] Reading Preferences card visible for:
  - Authors
  - Beta Readers
- [DONE] Form posts to update preferences
- [DONE] Values persist and reload correctly

**UAT**
- [DONE] Preferences saved and persisted across sessions
- [DONE] Applied correctly in Desktop Reader
- [DONE] Applied correctly in Mobile Reader
- [DONE] Font families visibly distinct
- [DONE] Font sizes visibly distinct
- [DONE] Works for both Author and Beta Reader roles

[DONE] Bug: https://draftview.co.uk/Reader/Read mobile view now goes 404

### Rename UserNotificationPreferences to UserPreferences — domain, repository, service, controller, viewmodels, views, tests (2026-04-10-1)

[DONE] Domain: UserPreferences entity, IUserPreferencesRepository, UserService methods  
[DONE] Added DisplayTheme (light/dark) to UserPreferences  

[DONE] Infrastructure:
- Replaced UserNotificationPreferencesConfiguration with UserPreferencesConfiguration
- Replaced UserNotificationPreferencesRepository with UserPreferencesRepository
- Updated DraftViewDbContext DbSet to UserPreferences

[DONE] Data migration:
- Added EF migration: RenameNotificationPreferencesToUserPreferences
- Renamed table NotificationPreferences → UserPreferences
- Renamed PK, FK, and index (UserId) constraints
- Updated DbContextModelSnapshot

[DONE] Application + consumers updated:
- DatabaseSeeder uses UserPreferences
- BetaBooksImporter creates UserPreferences for imported users
- ServiceCollectionExtensions DI updated to IUserPreferencesRepository → UserPreferencesRepository

[DONE] UI:
- Implemented DraftView.Light.css and DraftView.Dark.css
- Added theme toggle in Account/Settings
- Persisted DisplayTheme applied in _Layout.cshtml

[DONE] Tests:
- Web tests updated and passing for Account settings and theme persistence

[VERIFY]
- No remaining references to NotificationPreferences or UserNotificationPreferences (solution-wide search)
- Migration applied locally and existing data preserved
- Theme persists across sessions (save → logout → login)
- Seeder and importer correctly create default UserPreferences
- Production DB user has permission for db.Database.Migrate()

### Bugs resolved
- [DONE] Reader/Read comment box overflows page boundary on RHS — CSS fix (v2026-04-10-1)
- [DONE] AddComment POST on scene comment redirects to top of chapter — fixed, appends `#scene-{id}` anchor
- [DONE] AddComment POST on chapter-level comment redirects to top of page — fixed, appends `#chapter-comments` anchor
- [DONE] SetCommentStatus POST on chapter-level comment redirects to scene anchor — fixed, uses `#chapter-comments` when sceneId == chapterId
- [DONE] Reader/Read comment status dropdown missing — CommentStatus dropdown added for author/moderator on scene and chapter level
- [DONE] Author/Dashboard Recent Activity truncation — replaced on-the-fly assembly with persisted AuthorNotification; dismiss, clear all, viewport fix
- [DONE] Login always redirected to Reader/Dashboard regardless of role — author → Author/Dashboard, SystemSupport → Support/Dashboard

### Sprint 1 — Pre-Beta Push (Complete)
- [DONE] Fix prose font in reader view
- [DONE] Fix comment author display name — live lookup against AppUsers.DisplayName
- [DONE] Reactivate reader flow

### Sprint 2 — Reader Experience (Partial, 2026-04-10)
- [DONE] Fix scene-level Published labels in sections view
- [DONE] Fix Published Chapters sort order on dashboard — depth-first tree order (TDD)
- [DONE] Project switcher — sidebar in DesktopDashboard, query string selection, progress per project
- [DONE] Remember last selected project — query string naturally persists selection
- [DONE] Kindle-style resume on login — redirects to correct scene (exact scroll position → Sprint 2.5)
- [DONE] Persisted AuthorNotification entity — written at event time, dismiss single, clear all, 90-day prune, viewport fix (404 tests green)
- [DONE] Login redirect fix — author → Author/Dashboard, SystemSupport → Support/Dashboard
- [DONE] CommentStatus enum {New, AuthorReply, Ignore, Consider, Todo, Done, Keep}; SetStatus domain method and controller action in place
- [DONE] Author comment response UI — CommentStatus dropdown on Reader/Read (scene and chapter, author/moderator) and Author/Section

### Persisted AuthorNotifications detail (2026-04-10)
- [DONE] AuthorNotification domain entity (TDD, 7 tests)
- [DONE] IAuthorNotificationRepository + EF implementation (8 tests)
- [DONE] EF migration AddAuthorNotifications (composite index on AuthorId, OccurredAt)
- [DONE] DashboardService: GetNotificationsAsync (90-day prune), DismissNotificationAsync, DismissAllNotificationsAsync
- [DONE] CommentService writes NewComment / ReplyToAuthor notifications at event time
- [DONE] UserService writes ReaderJoined notification on AcceptInvitation
- [DONE] SyncService writes SyncCompleted notification on successful parse
- [DONE] AuthorController: DismissNotification + ClearAllNotifications POST actions
- [DONE] Dashboard.cshtml: count badge, Clear All button, per-item dismiss button
- [DONE] DraftView.Notifications.css: dismiss button styles, viewport panel height fix
- [DONE] Removed obsolete: GetRecentNotificationsAsync, GetRecentCommentsForDashboardAsync, GetRecentlyAcceptedAsync, GetRecentlySyncedAsync, CommentNotificationRow
- [DONE] 427 tests GREEN
- [DONE] 1 test skipped - it sends an email, which is now tested in SmtpEmailSenderIntegrationTests

### Email Sprint (2026-04-08)
- [DONE] Yahoo SMTP for dev; SmtpEmailSender provider-agnostic via appsettings.json
- [DONE] Production config: Oracle Email Delivery SMTP
- [DONE] Oracle Email Delivery: DKIM, SPF, approved senders; Cloudflare DKIM CNAME
- [DONE] Cloudflare email routing: support@draftview.co.uk → alastair_clarke@yahoo.com
- [DONE] SmtpEmailSender migrated to MailKit 4.15.1 (fixes Oracle STARTTLS auth)
- [DONE] MimeKit CVE-2026-30227 patched (4.7.1 → 4.15.1)
- [DONE] SQLite packages removed from solution
- [DONE] DraftView.Integration.Tests added; SmtpEmailSenderIntegrationTests green
- [DONE] ForgotPassword SMTP failure caught and logged
- [DONE] Dev-safe email addresses for Becca and Hilary

### Role Migration — Stages 1-4 (2026-04-06)
- [DONE] Stage 1: Identity roles as canonical source, web surface migrated
- [DONE] Stage 2: IAuthorizationFacade injected into UserService
- [DONE] Stage 3: SystemSupport role, SystemStateMessage entity + service + footer
- [DONE] Stage 4: System State Message Management UI on Support Dashboard
- [DONE] ReaderController secured (Author, BetaReader); SystemSupport isolated
- [DONE] HomeController role-based routing (Support → Author → Reader)
- [DONE] CSS versioning automation (Update-CssVersion.ps1)
- [DONE] UserService.DeactivateUserAsync revokes all ReaderAccess records (TDD)
- [DONE] Mobile reader flow — IsMobile(), MobileChapters/MobileScenes/MobileRead
- [DONE] Desktop reader views renamed to Desktop prefix

### Reader Authorization Model — FINAL DECISION
- Reader surface: Author + BetaReader only
- SystemSupport excluded from ReaderController; must use impersonation when needed
- No dual-role model; Author treated as elevated reader at runtime
- BaseReaderController: `[Authorize(Roles = "Author,BetaReader")]`

### Earlier Work
- [DONE] RtfConverter case-insensitive path fix; chapter ordering fix; email-as-nav-link
- [DONE] Account/Settings page; Dropbox panel for authors
- [DONE] User.UpdateDisplayName and UpdateEmail domain methods (TDD)
- [DONE] Sync file download progress — live file count, real percentage progress bar
- [DONE] LocalCachePath moved from user secrets to appsettings.json
- [DONE] Dual-list project assignment UI (ManageReaderAccess)
- [DONE] ReaderAccess entity + repository (TDD, migration)
- [DONE] Per-author Dropbox OAuth connection (DropboxConnection entity, IDropboxClientFactory)
- [DONE] IDropboxFileDownloader — full Dropbox sync working end to end in production
- [DONE] AuthorId added to ScrivenerProject (migration with backfill)
- [DONE] UseForwardedHeaders — fixes OAuth behind Nginx
- [DONE] Case-insensitive .scrivx lookup (Linux compatibility)
- [DONE] AddProjects background task (fixes 504 timeout)
- [DONE] BetaBooks importer: Comment.CreateForImport, DevTools command, 54 comments seeded
- [DONE] Becca Dunlop and Hilary Royston-Bishop accounts created with real emails
- [DONE] Toast notifications (fixed position, auto-dismiss, no layout shift)
- [DONE] Reply threading, comment edit and delete (including moderator delete)
- [DONE] PublishAsPartOfChapter domain invariant (TDD); chapter-only publishing enforced
- [DONE] CSS split into 7 files by concern; Heroicons as static C# class
- [DONE] Rebrand: DraftReader → DraftView throughout
- [DONE] pg.ps1, PowerShell.md, PRINCIPLES.md
- [DONE] Cloudflare SSL, Nginx, systemd service on Oracle Cloud VM
- [DONE] Floating footer: copyright, system status indicator
