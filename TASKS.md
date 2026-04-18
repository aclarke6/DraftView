# DraftView Task List
Last updated: 2026-04-18

---

## Test State

- 618 tests passing (1 skipped — SMTP integration test)
- Baseline after V-Sprint 1 complete + V-Sprint 2 Phase 1–3 + V-Sprint 3 Phase 3 + V-Sprint 4 Phase 1–3

---

## ARCHITECTURE RULES

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

### Error Handling Rule — MANDATORY
- If the user made a mistake, return a friendly error message with guidance.
- If the system failed to execute the action, treat it as a 500-style error, log it as an operational failure, and do not convert it into a user-facing workflow success-equivalent response.

---

## BUGS

- [DONE] Reader view does not apply saved Reading Preferences (font face and font size) — resolved 2026-04-17: moved read-view prose preference binding to read view models (`DesktopChapterReadViewModel` / `MobileReadViewModel`), populated in `ReaderController` read actions, and bound in `DesktopRead.cshtml` / `MobileRead.cshtml` via model-backed `data-prose-font` / `data-prose-font-size`
- [DONE] CS9107 in `AccountController` primary constructor (`IUserRepository userRepo` captured in derived type and passed to base) — resolved 2026-04-17: removed duplicate derived capture path by routing user lookups through base-owned repository helpers (`GetUserByIdAsync` / `GetUserByEmailAsync`), preserving behavior
- [OPEN] `/Author/InviteReader` submit fails with browser "This page isn't working" on production
  - observed at `https://draftview.co.uk/Author/InviteReader` when submitting the invite form
  - current behaviour: the request crashes instead of returning a controlled application error page or successful redirect
  - likely fault area: operational failure in invitation sending, configuration, or persistence after Sprint 4 error-handling changes
  - investigation focus: production logs for the failing request, SMTP/config state, and exception handling through the invite submission path
- [OPEN] Removing a reader from `/Author/Readers` does not remove the reader from the list
  - observed when using the remove action for both invited and active readers
  - current behaviour: the action completes but the reader remains visible in `/Author/Readers`
  - expected behaviour: the reader should be removed from the list, or the UI should clearly report why the removal was not applied
  - investigation focus: `AuthorController.SoftDeleteReader`, whether `SoftDeleteUserAsync` is actually called, and how `/Author/Readers` filters non-soft-deleted beta readers
- [OPEN] System Support has no readers page for Sprint 4 verification
  - observed while attempting to verify that System Support cannot see reader email addresses
  - current behaviour: there is no System Support page to view or inspect readers, so the negative-access check cannot be exercised through the UI
  - expected behaviour: either provide an explicit System Support readers surface with the correct deny-by-default email behaviour, or remove this UI verification path from Sprint 4 UAT if no such surface is intended
  - investigation focus: whether support should have a dedicated readers screen, a limited support operation flow, or no reader-list UI at all
- [OPEN] Reader settings shows protected-email decryption exception as an on-screen form error
  - observed when changing display name and new password in reader settings
  - current behaviour: the page shows `Ciphertext is not in the expected format`
  - expected behaviour: protected-email decryption failures should be treated as operational failures, logged, and shown through the controlled 500 error path rather than reflected as a user-facing settings validation message
  - investigation focus: `AccountController` settings actions, the controlled email retrieval path, and production rows with invalid `EmailCiphertext`

---

## Test state

- 498 Tests total
- One skipped test is `SmtpEmailSenderIntegrationTests` which sends a real email, so is not suitable for regular test runs but is included in the solution for manual execution when needed.
- Latest full passing count: 498 total, 497 passed, 1 skipped, 0 failed
- Latest targeted application count: 129 total, 129 passed, 0 skipped, 0 failed
- Latest targeted web count: 32 total, 32 passed, 0 skipped, 0 failed

---

# Sprint 4 — Email Privacy and Controlled Access (PHASED EXECUTION)

Ensure user email addresses are protected, not exposed to other users, and only accessible through controlled, auditable mechanisms. Align system behaviour with UK GDPR principles of data minimisation, access control, and protection by design.

Email handling model:
- Email stored in encrypted form
- Email lookup via deterministic HMAC of normalised email
- Encryption and decryption are performed through application/infrastructure services, not in the domain model
- Email never exposed in UI beyond explicitly permitted views
- Controlled access for administrative purposes only
- New views fail closed by default unless explicitly whitelisted

---

## Phase 7 — Audit and Security Hardening

**Audit Logging**
- [DONE] Log all privileged access attempts:
  - requesting user ID
  - target user ID
  - timestamp
  - success or failure
  - reason if provided
- [DONE] Audit the controlled email access seam itself
  - `ControlledUserEmailService` / `UserEmailAccessService` must emit an audit record for every allow or deny decision
  - audit coverage must include `SystemSupport` email-access requests, not just UI-layer actions
- [DONE] Ensure logs do NOT include plaintext email
  - remove plaintext email from `AccountController` login / invitation logs
  - remove plaintext email from `DatabaseSeeder` logs
  - `DatabaseSeeder` should not carry true email addresses in executable logging paths
  - replace any remaining email-based log placeholders with user IDs, role names, or other non-sensitive identifiers

**Access Control**
- [DONE] Enforce explicit permission for admin/support access
- [DONE] Ensure least privilege across system
  - review the current `SystemSupport` allow rule and tighten it from broad role-only access to an explicit privileged access policy if needed

**Security Tests**
- [DONE] Verify:
  - email not exposed in unauthorised views
  - logs contain no sensitive data
  - access rules enforced correctly
- [DONE] Add regression coverage for plaintext-email log prevention
  - source-level or focused behavioural tests should fail if `{Email}` logging reappears in protected flows
- [DONE] Add regression coverage for privileged email-access audit logging
  - tests should prove both allowed and denied access attempts are recorded

**Password reset clarification**
- [DONE] Password reset flow does not require email visibility after the user submits the address
  - user enters email on `ForgotPassword`
  - system verifies and sends the reset email
  - reset-link view shows only the password entry fields
  - no subsequent UI step requires the full email address to be displayed back to the user
  - password reset persistence should therefore avoid storing plaintext email solely for later display
  - password reset tokens now bind to `UserId` rather than storing plaintext email
  - one DB-backed web regression now creates an isolated user, requests a reset, completes the reset, and verifies login with the new password

---

## Phase 8 — Publish and Live Key Management

**Goal:** Deploy protected email handling safely to Linux/live environments

- [DONE] Verify compatibility with the real `Publish-draftview.ps1` workflow
  - publish requires a clean git state before deployment
  - publish copies fresh output to `/var/www/draftview`
  - publish restarts the `draftview` systemd service
- [DONE] Define how the live Linux environment provides persistent encryption key material
  - keys live in `appsettings.Production.json` on the server at `/var/www/draftview/`
  - this file is not part of the publish payload — it persists across deploys
- [DONE] Ensure live key material is stored outside the replaced publish payload where necessary
  - `appsettings.Production.json` is written directly on the server and never overwritten by `publish-draftview.ps1`
- [DONE] Ensure key material persists across app restarts and deployments
  - confirmed — `appsettings.Production.json` survives publish and service restart
- [DONE] Separate local/test key material from live key material
  - dev keys: .NET user-secrets on dev machine only, never committed
  - production keys: `appsettings.Production.json` on server only, never on dev machine
  - the two key sets are independent — production was seeded fresh on first deploy
- [DONE] Document the publish-time secret/key setup process
  - generate two 32-byte base64 keys on dev machine:
    `[Convert]::ToBase64String((1..32 | ForEach-Object { [byte](Get-Random -Max 256) }))`
  - SSH to production server
  - add `EmailProtection:EncryptionKey` and `EmailProtection:LookupHmacKey` to `appsettings.Production.json`
  - do not save production keys anywhere else
  - deploy via `publish-draftview.ps1`
- [DONE] Define operational guidance for key rotation and recovery
  - key rotation requires: generate new keys, re-encrypt all existing email ciphertexts, update `appsettings.Production.json`, restart service
  - no key rotation tooling exists yet — add to backlog if required
  - recovery: if keys are lost, existing encrypted emails are unrecoverable — users must reset email via support
- [DONE] Verify post-publish behaviour:
  - [DONE] login still works
  - [DONE] invitation links use the configured live base URL rather than localhost
  - [DONE] existing encrypted data remains decryptable after `Publish-draftview.ps1` completes and the service restarts

---

## UAT
- [DONE] User can register and login using email
- [DONE] Email not visible to other users
- [DONE] Authors cannot see beta reader email post-invite
- [DONE] User can view and update own email in settings
- [DONE] Admin/support access is restricted and logged
- [DONE] No regressions in authentication, invitation, or notification flows

---

## Compliance
- [DONE] Privacy notice updated and visible
- [DONE] Email usage limited to service-related communication
- [DONE] No marketing usage implemented
- [DONE] No third-party sharing for unrelated purposes
- [ ] Data handling aligns with UK GDPR and Data Protection Act 2018

---

## Post-publish UI testing (production)
- [DONE] Desktop: `/Account/Login` works with email and redirects to the correct dashboard
- [DONE] Desktop: `/Account/Settings` shows the current user email only in the whitelisted self-service view
- [DONE] Desktop: `/Author/Readers` does not expose beta reader email addresses
- [DONE] Desktop: `/Author/ManageReaderAccess/{userId}` does not expose stored email addresses
- [ ] Desktop: `/Author/InviteReader` sends an invitation whose email link uses the configured production `App:BaseUrl`
- [ ] Desktop: invitation acceptance flow completes without exposing stored email on non-whitelisted pages
- [ ] Desktop: forgot-password flow sends the reset email, allows password reset, and never re-displays the submitted email after request
- [ ] Desktop: support/system admin flows do not reveal user email unless explicitly authorised and auditable
- [DONE] Mobile: login works and redirects correctly
- [DONE] Mobile: reader dashboard and reading views do not expose stored email in navigation or page chrome
- [ ] Mobile: account settings remains the only self-service view showing the current user email
- [ ] Production smoke check: no page in the above journeys shows `localhost` invitation links, plaintext email leakage, or a friendly validation-style message for real operational failures

---

## Definition of Done
- [DONE] Governing regression tests created and verified RED at sprint start
- [DONE] Governing tests fully GREEN at sprint completion
- [ ] All Domain, Application, and Infrastructure changes developed via TDD
- [DONE] Review Sprint 4 infrastructure tests and decide which remain as permanent regression coverage versus which should be rewritten or removed as transitional implementation-lock tests
  - retained `ProtectedEmailPersistenceContractTests` as permanent regression coverage because it asserts the protected-email storage contract rather than transient implementation details
  - no Sprint 4 infrastructure persistence tests currently require rewrite/removal as transitional implementation-lock coverage
- [DONE] Full test suite passing
- [DONE] Green test count reported
- [DONE] No plaintext email stored in database
- [DONE] No email exposure in UI beyond whitelist
- [DONE] Audit logging verified
- [DONE] Migrations applied and validated
- [ ] Manual browser verification complete (desktop and mobile)
  - execute the production post-publish UI testing checklist above and record date/operator/result
- [ ] Changes committed with clear message
- [DONE] TASKS.md updated

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

---

## POST-VSPRINT-4 — Production Database Rebuild

Once V-Sprint 4 is complete and deployed, the production database must be rebuilt
so all published content is versioned correctly under the new methodology.

Current state: existing published sections have no `SectionVersion` records.
Readers are served via the `Section.HtmlContent` fallback path. Diff, classification,
and banner features have no data to operate against until a rebuild is done.

**Why after V-Sprint 4:** The first Republish after rebuild will automatically
classify each chapter. Rebuilding before V-Sprint 4 would require republishing twice.

**Rebuild sequence — run over SSH on production VM:**

Step 1 — SSH to production:
```
ssh -i C:\Users\alast\.ssh\draftview-prod.key ubuntu@193.123.182.208
```

Step 2 — Write and run the rebuild SQL via psql (password from appsettings.Production.json):
```bash
psql -U draftview -d draftview <<'EOF'
TRUNCATE "SectionVersions" CASCADE;
UPDATE "Sections" SET "IsPublished" = false, "PublishedAt" = null, "ContentChangedSincePublish" = false;
UPDATE "ReadEvents" SET "LastReadVersionNumber" = null, "BannerDismissedAtVersion" = null, "LastReadAt" = null;
EOF
```

Step 3 — Back on Windows, trigger a Dropbox sync for The Fractured Lattice project
from the Author dashboard to pull the latest Scrivener content into `Section.HtmlContent`.

Step 4 — Republish all chapters from Author/Sections. This creates `SectionVersion`
records for every document, anchors future comments, and populates `ChangeClassification`.

Step 5 — Verify readers can access the chapters and diff/banner features are operational.

**Note:** `BannerDismissedAtVersion` column only exists after V-Sprint 3 Phase 3 migration.
Do not run this rebuild until that migration has been applied to production.

# DraftView Publishing and Versioning Architecture (v4.3)
See `Publishing And Versioning Architecture.md` for the full architecture document including V-Sprints 1–10.
See `DraftView Git Rules.md` for branch strategy, gates, and commit standards.

- [x] V-Sprint 1 
    - [x] V-Sprint 1 Phase 1 — Domain + Infrastructure Foundation — 529 tests, 6 commits, migration applied 2026-04-17
    - [x] V-Sprint 1 Phase 2 — Section Tree Service + Import Provider — 558 tests, forensic review passed, committed 2026-04-17
    - [x] V-Sprint 1 Phase 3 — Versioning Application Layer — 567 tests, VersioningService with full TDD coverage, committed 2026-04-17
    - [x] V-Sprint 1 Phase 4 — Reader Content Source — 571 tests, version resolution and anchoring, committed 2026-04-17
    - [x] V-Sprint 1 Phase 5 — Author Republish UI — 575 tests, RepublishChapter action with TDD coverage, committed 2026-04-17
    - [x] V-Sprint 1 Phase 6 — Manual Upload UI — 575 tests, UploadScene GET/POST with form and button, committed 2026-04-17
    - [x] V-Sprint 1 — Core versioning backbone + manual upload — Republish → Version → Reader flow — COMPLETE
- [x] V-Sprint 2 — Paragraph diff highlighting — COMPLETE
    - [x] Phase 1 — Diff Engine (Domain) — 589 tests, `HtmlDiffService` with LCS paragraph diff, committed 2026-04-17
    - [x] Phase 2 — Application Diff Service — 596 tests, `SectionDiffService` coordinating version lookup and diff, committed 2026-04-17
    - [x] Phase 3 — Reader Highlighting — 596 tests, diff paragraphs rendered in desktop and mobile views, committed 2026-04-17
- [x] V-Sprint 3 — Reader experience layer — COMPLETE
    - [x] Phase 1 — Reader State — 602 tests, `LastReadAt` on `ReadEvent`, `RecordReadAsync` on `IReadingProgressService`, EF migration applied, committed 2026-04-18
    - [x] Phase 2 — Update Messaging — 602 tests, inline `scene-updated-notice` shown per scene when previously read and newer version exists, committed 2026-04-18
    - [x] Phase 3 — Update Banner — 602 tests, dismissible per-version banner, `BannerDismissedAtVersion` on `ReadEvent`, EF migration applied, committed 2026-04-18
- [ ] V-Sprint 4 — Pending change indicator and classification for authors
    - [x] Phase 1 — Change Classification Domain — 615 tests, `SetChangeClassification` on `SectionVersion` and `ChangeClassificationService` heuristic added, committed 2026-04-18
    - [x] Phase 2 — Classification Service Integration — 618 tests, `VersioningService.RepublishChapterAsync` now classifies changes from previous version diff and persists advisory classification, committed 2026-04-18
    - [x] Phase 3 — Author UI Indicator — 618 tests, advisory chapter-level change indicator (Polish/Revision/Rewrite) shown next to Republish when unpublished changes exist, committed 2026-04-18
    - [ ] Phase 1 — Change Classification Domain — `IChangeClassificationService`, `ChangeClassificationService`, `SetChangeClassification` on `SectionVersion`
    - [ ] Phase 2 — Classification Service Integration — wire into `VersioningService.RepublishChapterAsync`
    - [ ] Phase 3 — Author UI Indicator — colour-coded Polish/Revision/Rewrite label on Sections view
- [ ] V-Sprint 5 — AI summaries — named characters and locations, editable before publish
- [ ] V-Sprint 6 — Per-document publishing and dedicated Publishing Page
- [ ] V-Sprint 7 — Scheduling and locking
- [ ] V-Sprint 8 — Dropbox incremental sync
- [ ] V-Sprint 9 — Version retention and deletion
- [ ] V-Sprint 10 — Tree builder UI (Option A, post-launch)

---

## BACKLOG — Go-Live Requirements

### Key Management — Pre Go-Live

- [ ] Copy production `EmailProtection:EncryptionKey` and `EmailProtection:LookupHmacKey` values into a secure password manager (Bitwarden, 1Password, or equivalent) — entry titled "DraftView Production Email Keys"
- [ ] Consider moving keys from `appsettings.Production.json` to systemd environment variables in `/etc/systemd/system/draftview.service` — removes all secrets from the web root entirely
- [ ] Confirm a second person or secure location holds the recovery keys — single point of knowledge is a single point of failure
- [ ] Document recovery procedure: if keys are lost, existing encrypted emails are unrecoverable — users must contact support to reset email manually

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

# DraftView Task List

## Incremental Refactor Roadmap (Staged, Codebase-Specific)

This roadmap replaces the previous high-level Phase 1–5 architecture bullets.  
Each phase is independently executable, low-risk, and grounded in current DraftView hotspots: controller guard duplication, procedural controller workflows, startup/seeding complexity, and sync kickoff patterns.

---

## Phase 1 — Centralise controller user/role resolution (low-risk, no behaviour change)

**Targets:**

- [DONE] Consolidate repeated author guard logic currently duplicated in:
  - `AuthorController.GetAuthorAsync`
  - `DropboxController.GetAuthorAsync`
  - direct `User.Identity?.Name` reads in controller actions
- [DONE] Introduce shared helpers in `BaseController`:
  - `TryGetCurrentAuthorAsync`
  - `RequireCurrentAuthorAsync`
- [DONE] Replace per-controller guard copies with the shared pattern.

**Outcome:**

- Single authoritative path for resolving the current author.
- Removal of repeated guard logic across controllers.
- No functional changes; pure consolidation.

---

## Phase 2 — Extract Procedural Controller Workflows (Step-by-Step)

### Step 1 — Extract Reader Access Update Flow (lowest risk)

**Scope:**
- `AuthorController.UpdateReaderAccess`

---

### Architecture Placement

- Create interface in:
  - `DraftView.Application.Interfaces.Services`

- Create implementation in:
  - `DraftView.Application.Services`

- This is an **Application-layer orchestration service**
- Repositories and `IUnitOfWork` must be injected
- No persistence implementation may be placed in the service

---

### TDD Sequence (mandatory)

#### 1. Create the interface

Create:

`IAuthorReaderAccessService`

Method:

```csharp
Task UpdateReaderAccessAsync(
    Guid authorId,
    Guid readerId,
    List<Guid> grantIds,
    List<Guid> revokeIds,
    CancellationToken ct = default);

#### 2. Create the application service stub

Create implementation:

AuthorReaderAccessService

Initial method body must be:

throw new NotImplementedException();

#### 3. Create failing tests first

Create Application-layer tests covering:

grants access when no access record exists
reinstates access when an inactive access record exists
revokes access when an active access record exists
persists changes once via IUnitOfWork.SaveChangesAsync()
does not fail when revoke target does not exist

Tests must:

follow AAA structure
verify behaviour only
fail before implementation is written
4. Implement to green

Move the orchestration logic from AuthorController.UpdateReaderAccess into the service:

For each grantId:
check existing access via IReaderAccessRepository
if none, create via ReaderAccess.Grant(...)
if existing and inactive, call Reinstate()
For each revokeId:
load existing access
call Revoke() if present
persist once via IUnitOfWork.SaveChangesAsync(ct)
5. Refactor controller after tests pass

Replace the orchestration block in AuthorController.UpdateReaderAccess with a service call.

Controller should only:

resolve author using RequireCurrentAuthorAsync
call IAuthorReaderAccessService.UpdateReaderAccessAsync(...)
set TempData
redirect

No loops, no repository calls, no domain mutation should remain in the controller.

Result
Application orchestration is moved behind a dedicated service
Behaviour is defined by tests before implementation
Controller becomes a thin HTTP surface
No functional change

---

### Step 2 — Extract Section Query Assembly

**Scope:**
- `AuthorController.Section`

**Actions:**
- Create a query-focused service (e.g. `IAuthorSectionQueryService`)
- Move all data assembly into the service:
  - section lookup
  - parent resolution
  - comments retrieval
  - read events
  - user display name mapping
  - view model construction
- Service returns a ready-to-render model or DTO

- Controller becomes:
  - resolve author
  - call service
  - return `NotFound()` or `View(model)`

**Result:**
- Controller stops assembling complex view models
- Query logic becomes reusable and testable

---

### Step 3 — Extract Project Onboarding Workflow

**Scope:**
- `AuthorController.AddProjects`

**Actions:**
- Create a dedicated orchestration service (e.g. `IAuthorProjectOnboardingService`)
- Move all workflow logic into the service:
  - discovery lookup
  - filtering selected projects
  - soft-delete restore vs create decision
  - duplicate handling
  - persistence
  - initial sync triggering decision (not execution mechanism)

- Service returns a result object:
  - count added
  - project names
  - any warnings/failures

- Controller becomes:
  - resolve author
  - validate input
  - call service
  - map result to `TempData`
  - redirect

**Result:**
- Controller no longer contains multi-step orchestration
- Project onboarding becomes a cohesive application workflow

---

### Step 4 — Standardise Controller Shape

**Apply to all affected actions:**

Controllers should follow this structure only:

1. Resolve user / author  
2. Validate input  
3. Call service  
4. Map result to UI (TempData / ViewModel)  
5. Return response  

**Explicitly remove:**
- loops over domain entities  
- branching business rules  
- repository coordination  
- multi-step workflows  

---

## Outcome

- Controllers become thin HTTP adapters only  
- All business workflows live in application services  
- Logic becomes:
  - testable without MVC  
  - reusable across entry points  
  - composable for future features  

- Clear separation achieved:
  - Controller → request/response  
  - Service → orchestration and rules  

---

## Phase 3 — Decompose startup/seeding

**Targets:**

- Break down `DatabaseSeeder.SeedAsync` into smaller, isolated steps.
- Move orchestration out of `WebApplicationExtensions`.
- Introduce clear boundaries between:
  - seeding
  - runtime configuration
  - environment-specific initialisation

**Outcome:**

- Startup becomes predictable and minimal.
- Seeding logic becomes explicit and testable.

---

## Phase 4 — Standardise inheritance and shared utilities

**Targets:**

- Reduce repeated query/DTO loops across:
  - `ReaderController`
  - `BaseReaderController`
- Consolidate shared reader-side patterns into a single abstraction.

**Outcome:**

- Less duplication.
- Clearer inheritance boundaries.
- Consistent reader-side behaviour.

---

## Phase 5 — Extract remaining procedural workflows

**Targets:**

- Identify remaining controller-heavy procedural flows.
- Extract into services following the Phase 2 pattern.
- Ensure controllers become orchestration surfaces only.

**Outcome:**

- Controllers become uniformly thin.
- All business logic moves to services.

---

## Phase 6 — Standardise sync kickoff (remove inline Task.Run)

**Targets:**

- Replace inline `Task.Run` sync kickoff patterns with:
  - background queue
  - or dedicated sync service
- Standardise sync initiation across controllers.

**Outcome:**

- Predictable, testable sync initiation.
- No more inline fire-and-forget patterns.


# DONE (this project)

### Additional Completed Tasks

- [DONE] Layout top bar now shows the current user display name instead of email; falls back to `Account settings` when display name is missing, with hover text `Account settings`
- [DONE] Audited `Views/Shared/_Layout.cshtml` for style leakage while fixing the mobile nav toggle placeholder; no additional style leakage required CSS changes
- [DONE] Audited `Views/Author/Dashboard.cshtml` for style leakage while fixing the mobile projects actions layout; replaced inline actions-row styling with dashboard CSS classes
- [DONE] Improved mobile portrait dashboard table affordances by preserving card boundaries and adding rotate-to-landscape guidance on author dashboard cards
- [DONE] Fixed style leakage by scoping prose font preferences to reader surfaces only so system UI remains on standard typography
- [DONE] Fixed style leakage in `Views/Author/InviteReader.cshtml` by replacing inline layout styling with dashboard CSS classes while adding the invite display-name field
- [DONE] Fixed reader diff UX for removed paragraphs — removed paragraphs now render as thin visual markers instead of strikethrough deleted text, allowing beta readers to focus on published content rather than editorial changes (2026-04-18)
- [DONE] Sprint 4 Phase 6 end-to-end integration is complete
  - fixed configuration-backed protected-email keys are in place for dev and testing
  - DB-backed real-host regression coverage now exists for login, password reset, and invitation provisioning
  - issuing the same invite twice now supersedes the older pending invite with a fresh token
  - invitation and password-reset flows now prove protected persistence rather than plaintext persistence
  - DB-backed web regressions override `IEmailSender` to use `ConsoleEmailSender`, so tests do not depend on live SMTP
  - latest targeted web verification GREEN: 34 passed, 0 failed
  - latest full-suite verification GREEN: 481 total, 480 passed, 1 skipped, 0 failed

## Sprint 4 — Email Privacy and Controlled Access (PHASED EXECUTION)

Ensure user email addresses are protected, not exposed to other users, and only accessible through controlled, auditable mechanisms. Align system behaviour with UK GDPR principles of data minimisation, access control, and protection by design.

Email handling model:
- Email stored in encrypted form
- Email lookup via deterministic HMAC of normalised email
- Encryption and decryption are performed through application/infrastructure services, not in the domain model
- Email never exposed in UI beyond explicitly permitted views
- Controlled access for administrative purposes only
- New views fail closed by default unless explicitly whitelisted

## Phase 1 [DONE] — Rules and Governing Red Tests

- [DONE] Defined the whitelist for stored-email rendering, restricted to `Views/Account/Settings.cshtml`
- [DONE] Added governing source-level and rendered-output email-exposure regressions for non-whitelisted pages
- [DONE] Seeded initial red-state evidence for `AcceptInvitation`, `ManageReaderAccess`, `Readers`, and shared layout leakage
- [DONE] Added a behavioural `/Account/Login` web regression to preserve login flow during later email-model changes
- [DONE] Confirmed sprint start state: email-exposure governing tests were RED and behavioural guard tests were expected GREEN

## Phase 2 [DONE] — Web Surface Cleanup (Fast Feedback)

- [DONE] Removed non-whitelisted stored-email display from `AcceptInvitation`, `ManageReaderAccess`, `Readers`, and shared layout navigation
- [DONE] Stopped `AccountController.AcceptInvitation` GET from passing stored email into a non-whitelisted rendered view
- [DONE] Confirmed other reviewed non-whitelisted controller/view paths no longer pass stored email for display
- [DONE] Made the source-level and rendered-output email-exposure governing tests GREEN
- [DONE] Kept the `/Account/Login` behavioural regression GREEN throughout cleanup

## Phase 3 [DONE] — Infrastructure (Data Shape First)

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

## Phase 4 [DONE] — Application Layer (Behaviour and Access Control)

- [DONE] Added the application seams for controlled email access and protected login lookup
  - `IUserEmailAccessService`
  - `IUserEmailProtectionService`
  - `IControlledUserEmailService`
  - `IAuthenticationUserLookupService`
- [DONE] Implemented deny-by-default application access rules
  - self access allowed
  - explicit `SystemSupport` administrative access allowed
  - author access to reader stored email denied once an invitation has been sent
  - all other access denied by default
- [DONE] Enforced application-layer orchestration
  - access check occurs before decryption
  - protected email is resolved only after approval
  - migrated self-service `AccountController` email flows onto the application seam
  - consolidated `AccountController` current-user lookup logic behind one helper
- [DONE] Migrated authentication role redirect lookup onto the protected login seam
  - `AccountController.Login` now uses `IAuthenticationUserLookupService`
  - sign-in mechanics were left unchanged
- [DONE] Reviewed privileged support/admin email reveal needs
  - no concrete `SystemSupport` consumer currently requires revealing another user's stored email
  - no unused privileged reveal path was added
- [DONE] Completed application-layer test coverage and verification
  - targeted application tests covered access control, decryption orchestration, deny-by-default behaviour, and authentication lookup
  - targeted controller tests covered settings, self-service email/password flows, and login redirect seam usage
  - full-suite verification has since been restored GREEN: 466 total, 465 passed, 1 skipped, 0 failed

---

## Phase 5 [DONE] — Domain Refinement

- [DONE] Confirmed the intended domain boundary for protected email state without moving crypto or persistence concerns into the domain
- [DONE] Tightened `User` invariants around protected email state while keeping runtime email loading explicit and non-persistent
- [DONE] Added focused domain tests to prove valid protected state is accepted and invalid protected state is rejected
- [DONE] Re-audited the boundary and confirmed no wider domain refactor was required for Sprint 4
- [DONE] Targeted verification GREEN: `dotnet test DraftView.Domain.Tests --nologo --filter "FullyQualifiedName~DraftView.Domain.Tests.Entities.UserTests"` returned 42 passed, 0 failed

---

## Phase 6 [DONE] — End-to-End Integration

**Goal:** Ensure system flows work with new model

**Prerequisite: Stabilise dev key material**
- [DONE] Step 0.1: Replace transient dev protected-email keys with fixed configuration-backed keys
  - add one fixed dev encryption key
  - add one fixed dev lookup-HMAC key
  - store both in `.NET` user secrets for `DraftView.Web`
  - fail fast if either key is missing or invalid
- [DONE] Step 0.2: Repair local dev user email state under the fixed keys without dropping project data
  - existing dev protected email generated with transient keys must be replaced
  - selectively rebuild `AppUsers` protected email state under the fixed keys
  - update matching Identity email/login values where required
  - confirm dev `Author` and `SystemSupport` users can still log in

**High-level steps**
- [DONE] Step 1: Confirm which end-to-end outcomes are already covered by Phases 3–5
  - avoid duplicating infrastructure contract coverage at integration level
  - limit Phase 6 to real remaining flow gaps
- [DONE] Step 2: Add one DB-backed login integration proof
  - real web host boots in `Testing`
  - login still succeeds under the protected lookup wiring
- [DONE] Step 3: Add one invitation/provisioning integration proof
  - author invite flow now runs through the real web host in `Testing`
  - issuing the same invite twice proves older pending invites are superseded by a fresh token
  - invitation-related user provisioning persists protected email fields through the real stack
  - do not invent a standalone registration flow if the product does not actually expose one
  - before Sprint 4 is complete, sending a fresh invitation must supersede any older pending invite for the same target user
  - keep the author workflow simple: if an invite is not received, the author just issues a new invite
  - implement this in the existing invitation send path rather than adding resend/cancel/reissue UI
- [DONE] Step 4: Add one flow-level no-plaintext-persistence assertion
  - verify the relevant persisted user row stores protected values rather than plaintext email
  - keep this compact and complementary to infrastructure contract tests
- [DONE] Step 5: Re-run governing and full-suite verification
  - confirmed no end-to-end regression in the protected login path
  - latest full-suite verification GREEN: 480 total, 479 passed, 1 skipped, 0 failed

  ---

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
- [DONE] Author/Dashboard Recent Activity truncation — replaced on-the-fly assembly with persisted AuthorNotification; dismiss, clear all, 90-day prune, viewport fix
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

# Extracting Scrivener Project naming and refactoring it
# ScrivenerProject → Project Rename — Run List

Run the full test suite after every item. Do not proceed if tests are red.

---

## Stage 1 — Domain Entity

- [Done] Rename class `ScrivenerProject` → `Project` (entity file rename + class name)
- [Done] Rename file `ScrivenerProject.cs` → `Project.cs`
- [Done] Rename property `Project.ScrivenerRootUuid` → `Project.SyncRootId`
- [None Located] Rename invariant code string `"I-PROJ-"` references if they mention Scrivener — none found, codes are generic
- [Passed] **Run tests** ✓

---

## Stage 2 — Domain Repository Interface

- [Done] Rename interface `IScrivenerProjectRepository` → `IProjectRepository`
- [Done] Rename file `IScrivenerProjectRepository.cs` → `IProjectRepository.cs`
- [Done] Rename method `GetSoftDeletedByScrivenerRootUuidAsync()` → `GetSoftDeletedBySyncRootIdAsync()`
- [Passed] **Run tests** ✓

---

## Stage 3 — Domain Service Interfaces

- [Done] Rename interface `IScrivenerProjectDiscoveryService` → `IProjectDiscoveryService`
- [Done] Rename file `IScrivenerProjectDiscoveryService.cs` → `IProjectDiscoveryService.cs`
- [Done] Rename property `DiscoveredProject.ScrivenerRootUuid` → `DiscoveredProject.SyncRootId`
- [Passed] **Run tests** ✓

---

## Stage 4 — Infrastructure Configuration

- [Done] Rename class `ScrivenerProjectConfiguration` → `ProjectConfiguration`
- [Done] Rename file `ScrivenerProjectConfiguration.cs` → `ProjectConfiguration.cs`
- [Passed] **Run tests** ✓

---

## Stage 5 — Infrastructure Repository

- [Done] Rename class `ScrivenerProjectRepository` → `ProjectRepository`
- [Done] Rename file `ScrivenerProjectRepository.cs` → `ProjectRepository.cs`
- [Done] Rename method `GetByScrivenerRootUuidAsync()` → `GetBySyncRootIdAsync()`
- [redundant] Rename method `GetSoftDeletedByScrivenerRootUuidAsync()` → `GetSoftDeletedBySyncRootIdAsync()`
- [ ] Update all internal `p.ScrivenerRootUuid` → `p.SyncRootId` in query lambdas
- [ ] **Run tests** ✓

---

## Stage 6 — Application Discovery Service

> `ScrivenerProjectDiscoveryService` discovers projects by scanning for `.scrivx` files
> and parsing Scrivener vault structure. It is Scrivener-specific. The class name is
> correct and stays. Only the repository parameter type needs updating.

- [Done] Confirm class name remains `ScrivenerProjectDiscoveryService` — do NOT rename
- [Done] Confirm file name remains `ScrivenerProjectDiscoveryService.cs` — do NOT rename
- [Done] Update constructor parameter type `IScrivenerProjectRepository` → `IProjectRepository` (if not already cascaded from Stage 2)
- [Done] Confirm all `SyncRootId` references correct (were `ScrivenerRootUuid`, updated in Stage 3)
- [passed] **Run tests** ✓

---

## Stage 7 — Application SyncService

- [Done] Update constructor parameter type `IScrivenerProjectRepository` → `IProjectRepository`
- [Done] Update `nameof(ScrivenerProject)` → `nameof(Project)` in exception throw
- [Done] Confirm all `project.SyncRootId` references correct (were `ScrivenerRootUuid`, cascaded from Stage 1)
- [Passed] **Run tests** ✓

---

## Stage 8 — Web ViewModels

- [Done] Update `DashboardViewModel.ActiveProject` type: `ScrivenerProject?` → `Project?`
- [Done] Update `DashboardViewModel.AllProjects` type: `IReadOnlyList<ScrivenerProject>` → `IReadOnlyList<Project>`
- [Done] Update `ReaderAccessViewModel.ProjectsWithAccess` type: `IReadOnlyList<ScrivenerProject>` → `IReadOnlyList<Project>`
- [Done] Update `ReaderAccessViewModel.ProjectsWithoutAccess` type: `IReadOnlyList<ScrivenerProject>` → `IReadOnlyList<Project>`
- [Passed] **Run tests** ✓

---

## Stage 9 — Web AuthorController

- [Done] Update constructor parameter type `IScrivenerProjectRepository` → `IProjectRepository`
- [Done] Update constructor parameter type `IScrivenerProjectDiscoveryService` → `IProjectDiscoveryService`
- [Done] Update background task variable type `IScrivenerProjectRepository` → `IProjectRepository`
- [Done] Update `ScrivenerProject.Create()` → `Project.Create()`
- [Done] Confirm all `p.SyncRootId` and `d.SyncRootId` references correct (were `ScrivenerRootUuid`)
- [Done] Update `GetSoftDeletedByScrivenerRootUuidAsync()` call → `GetSoftDeletedBySyncRootIdAsync()`
- [Passed] **Run tests** ✓

---

## Stage 10 — Web DI Registration

- [Done] Update registration: `ScrivenerProjectRepository` → `ProjectRepository`
- [Done] Update registration: `IScrivenerProjectRepository` → `IProjectRepository`
- [Done] Update registration: `IScrivenerProjectDiscoveryService` → `IProjectDiscoveryService`
- [Done] Confirm `ScrivenerProjectDiscoveryService` registration stays — it is the correct Scrivener-specific implementation of `IProjectDiscoveryService`
- [Passed] **Run tests** ✓

---

## Stage 11 — Test Files

- [Done] Rename file `ScrivenerProjectTests.cs` → `ProjectTests.cs`
- [Done] Rename class `ScrivenerProjectTests` → `ProjectTests`
- [Done] Update all `ScrivenerProject.Create()` → `Project.Create()`
- [Done] Update all `ScrivenerProject` type references
- [Done] Rename file `ScrivenerProjectRepositoryTests.cs` → `ProjectRepositoryTests.cs`
- [Done] Rename class `ScrivenerProjectRepositoryTests` → `ProjectRepositoryTests`
- [Done] Update field `ScrivenerProjectRepository _sut` → `ProjectRepository _sut`
- [Done] Update helper `MakeProject()` — `ScrivenerProject.Create()` → `Project.Create()`
- [Done] Update `SyncServiceTests` field type `Mock<IScrivenerProjectRepository>` → `Mock<IProjectRepository>`
- [Passed] **Run tests** ✓
---

## Stage 12 — EF Migration

- [Done] Run: `dotnet ef migrations add RenameScrivenerProjectToProject --project DraftView.Infrastructure --startup-project DraftView.Web`
- [Done] Review generated migration — if EF generates DropTable/CreateTable, rewrite as `migrationBuilder.RenameTable()` to preserve data
- [Done] Run: `dotnet ef database update --project DraftView.Infrastructure --startup-project DraftView.Web`
- [Passed] **Run tests** ✓

---

## Stage 13 — Solution-Wide Verification

- [Done] Solution-wide search for `ScrivenerProject` — only permitted hits: `ScrivenerProjectDiscoveryService`, `IScrivenerProjectParser`, and migration history
- [Done] Solution-wide search for `ScrivenerRootUuid` — zero hits outside migration history
- [Done] Solution-wide search for `IScrivenerProjectRepository` — zero hits
- [Done] Solution-wide search for `IScrivenerProjectDiscoveryService` — zero hits
- [Done] **Run full test suite** ✓
- [Done] Commit: `refactor: rename ScrivenerProject to Project, IProjectRepository replaces IScrivenerProjectRepository, SyncRootId replaces ScrivenerRootUuid`

---

## Known Debt — Not in This Sprint

Deliberately left for the sync extraction sprint:

- `Section.ScrivenerUuid` — moving off `Section` entirely into `ScrivenerSyncMapping`
- `Section.ScrivenerStatus` + `UpdateScrivenerStatus()` — Scrivener display metadata, name is honest
- `IScrivenerProjectParser` — parses `.scrivx`, genuinely Scrivener-specific
- `ParsedBinderNode.ScrivenerStatus` — Scrivener binder metadata
- `ScrivenerProjectDiscoveryService` — Scrivener-specific implementation of `IProjectDiscoveryService`, name is correct

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
