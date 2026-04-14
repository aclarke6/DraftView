# DraftView Task List
Last updated: 2026-04-12

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

- [DONE] Step 1: Define the infrastructure persistence contract for protected email storage
  - `AppUsers.Email` plaintext persistence is to be replaced by `EmailCiphertext` and `EmailLookupHmac`
  - stored email value must be encrypted at rest; plaintext email must not be written to `AppUsers`
  - lookup must use deterministic HMAC of normalised email for equality checks and uniqueness
  - infrastructure must support repository lookup and existence checks without querying plaintext email
  - encryption and decryption must be performed through application/infrastructure services, not by the domain model
  - encryption and HMAC key material must be kept outside the database
- [DONE] Step 2: Write failing infrastructure tests for:
  - encryption does not store plaintext
  - decryption restores original value
  - HMAC lookup is deterministic
  - different inputs produce different lookup values
  - no plaintext email is persisted for new or updated users
- [DONE] Step 3: Add schema fields:
  - `EmailCiphertext`
  - `EmailLookupHmac`
- [DONE] Step 4: Add a unique index for `EmailLookupHmac`
- [DONE] Step 5: Apply the migration
- [DONE] Step 5.5 Bug Fix: Make the protected-email migration safe for populated databases
  - current migration fails on existing data because `EmailLookupHmac` is added as `NOT NULL DEFAULT ''`
  - the unique index then fails because multiple existing rows receive the same empty value
  - revised the migration so existing rows are backfilled with distinct placeholder protected values before uniqueness is enforced
  - demonstrate the fix by applying the migration successfully to a non-empty local database
- [ ] Step 6: Introduce the email-encryption seam with correct layer ownership
  - application owns the contract for email encryption/decryption
  - infrastructure owns the concrete implementation
  - domain owns none of the encryption or decryption mechanics
- [DONE] Step 6.1: Define the application-layer contract for protected email encryption/decryption
- [DONE] Step 6.2: Write failing tests for encryption/decryption behaviour before implementation
  - encrypting email must not return plaintext
  - decrypting ciphertext must restore the original normalised email
  - invalid ciphertext must fail safely
  - no domain type performs encryption or decryption directly
- [DONE] Step 6.3: Add temporary `NotImplementedException` stubs if required to compile the new seam before implementation
- [DONE] Step 6.4: Implement the infrastructure encryption service using ASP.NET Core Data Protection or equivalent
- [DONE] Step 6.5: Register the service and get the Step 6 tests GREEN without yet changing lookup logic
  - local/dev registration only at this step
  - do not fold Linux/live key provisioning into this implementation step
- [DONE] Step 6 review: Step 6 encryption seam is proven GREEN; remaining infrastructure failures belong to plaintext persistence and lookup replacement work
- [ ] Step 7: Introduce the email-HMAC lookup seam with correct layer ownership
  - application owns the contract for deterministic email lookup HMAC generation
  - infrastructure owns the concrete implementation
  - domain owns none of the HMAC mechanics
- [DONE] Step 7.1: Define the application-layer contract for deterministic email lookup HMAC generation
- [DONE] Step 7.2: Write failing tests for HMAC lookup behaviour before implementation
  - same normalised email must produce the same lookup value
  - different normalised emails must produce different lookup values
  - lookup output must not equal plaintext email
  - no domain type performs HMAC generation directly
- [DONE] Step 7.2.5 Bug Fix: Tighten the domain-boundary HMAC test so it detects HMAC mechanics, not the presence of the `EmailLookupHmac` property name
  - current test is overbroad and fails on naming alone
  - the test must check for crypto implementation concerns in the domain model, not protected-field naming
- [DONE] Step 7.2.5 review: the current red state now shows only the expected missing-HMAC-service failures plus the separate plaintext-persistence failures
- [DONE] Step 7.3: Add temporary `NotImplementedException` stubs if required to compile the new seam before implementation
- [DONE] Step 7.4: Implement the infrastructure HMAC lookup service using secure key material kept outside the database
- [DONE] Step 7.5: Register the service and get the Step 7 tests GREEN without yet changing repository queries or persistence mapping
- [DONE] Step 7 review: Step 7 HMAC seam is proven GREEN; remaining infrastructure failures belonged to plaintext persistence replacement work
- [DONE] Step 8: Wire persistence so plaintext email is no longer stored
  - Step 8.1: Stop EF from mapping plaintext `User.Email`
  - Step 8.2: Generate `EmailCiphertext` and `EmailLookupHmac` during save
  - Step 8.3: Rehydrate runtime `User.Email` from ciphertext on repository reads
  - Step 8.4: Replace plaintext repository lookups with HMAC lookups
  - Step 8.5: Patch direct `DbContext` email queries used during startup/seeding
- [DONE] Step 9: Get the infrastructure tests green
- [DONE] Step 9.5 Bug Fix: Restore full-suite compatibility after the `DraftViewDbContext` constructor change
  - current regression breaks `DraftView.Web.Tests.Controllers.AccountControllerTests`
  - Moq can no longer proxy `DraftViewDbContext` with the constructor shape those tests expect
  - fix the regression without undoing the protected-email persistence behaviour
  - `dotnet test --nologo` returned GREEN: 449 total, 448 passed, 1 skipped, 0 failed
- [ ] Step 10: Refactor and review Phase 3 with tests still green
  - review schema, migration, encryption, HMAC, and persistence changes as one coherent Phase 3 unit
  - remove any low-value transitional plumbing introduced during Phase 3
  - confirm no plaintext email persistence or plaintext infrastructure lookup path remains
  - keep `DraftView.Infrastructure.Tests` GREEN throughout
  - do not widen scope into Phase 4 application access-control work during this cleanup
- [ ] Step 10.1: Freeze the behavioural baseline
  - run `dotnet test DraftView.Infrastructure.Tests --nologo`
  - run `dotnet test --nologo`
  - treat this as the green baseline before any Phase 3 refactor
- [ ] Step 10.2: Review and rename Phase 3 seams for clarity where the result is materially better
- [ ] Step 10.3: Extract helpers from `DraftViewDbContext` where this improves cohesion and reduces long blocks
- [ ] Step 10.4: Remove duplication in the normalize -> HMAC -> encrypt -> protect flow
- [ ] Step 10.5: Review repository hydration code and extract shared helpers where it improves readability
- [ ] Step 10.6: Review `DatabaseSeeder` for Phase 3 duplication and extract small local helpers where useful
- [ ] Step 10.7: Review `DraftViewDbContext` constructor and fallback behaviour for cleaner architecture
- [ ] Step 10.8: Remove low-value transitional plumbing introduced only to get Phase 3 green
- [ ] Step 10.9: Re-audit Phase 3 architecture boundaries
  - infrastructure owns persistence, encryption, HMAC, save-time protection, and protected lookup
  - domain owns no crypto or persistence mechanics
  - application contracts remain small and explicit
- [ ] Step 10.10: Final Phase 3 verification
  - run `dotnet test DraftView.Infrastructure.Tests --nologo`
  - run `dotnet test --nologo`
  - update task state only after refactor and verification are complete

---

## Phase 4 — Application Layer (Behaviour and Access Control)

**Goal:** Control access and orchestrate protection

**Services**
- [ ] Introduce `IUserEmailProtectionService`
- [ ] Introduce `IUserEmailAccessService`
- [ ] Introduce explicit authentication lookup seam for resolving a user from login email input via protected lookup

**Access Rules**
- [ ] Self access permitted
- [ ] Authorised admin/support access permitted
- [ ] All other access denied (deny-by-default)

**Orchestration**
- [ ] Authorisation check occurs before decryption
- [ ] Decrypt only after approval
- [ ] Centralise all email access through service
- [ ] Route login identity resolution through the protected authentication lookup seam rather than plaintext email lookup

**Application TDD**
- [ ] Write failing tests:
  - self access allowed
  - unauthorised access denied
  - authorised access allowed
  - decrypt not called when access denied
- [ ] Create regression test: email access is denied by default unless authorised
- [ ] Write failing lower-level integration/authentication regression test:
  - authentication lookup resolves the correct user from login email input via protected lookup
  - do not bind this test to `IUserRepository.GetByEmailAsync`
- [ ] Implement to green
- [ ] Refactor

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
