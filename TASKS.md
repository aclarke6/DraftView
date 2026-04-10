# DraftView Task List
Last updated: 2026-04-10

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
Every script that modifies any `.css` file must also bump `--css-version` in `DraftView.Core.css`. [Done] CSS versioning automated via Update-CssVersion.ps1

Always use regex replace so it matches whatever version is currently there — never hardcode the expected current value:
```powershell
$core = $core -replace '--css-version: "v[^"]+";', '--css-version: "v2026-04-02-1";'
if ($core -notmatch 'v2026-04-02-1') { Write-Host "ERROR: bump failed" -ForegroundColor Red; exit 1 }
```

### Controller Action Guards — MANDATORY
Every controller that accesses data or performs mutations must be protected by an
`[Authorize]` attribute at class level, using the appropriate role or policy:

- `AuthorController`  → `[Authorize(Policy = "RequireAuthorPolicy")]`
- `ReaderController`  → `[Authorize(Roles = "Author,BetaReader")]` (via `BaseReaderController`)
- `SupportController` → `[Authorize(Roles = "SystemSupport")]`
- `DropboxController` → `[Authorize(Policy = "RequireAuthorPolicy")]`
- `AccountController` → individual actions use `[Authorize]` where needed

Audit pattern — verify class-level attributes are present:
```powershell
Get-ChildItem "C:\Users\alast\source\repos\DraftView\DraftView.Web\Controllers" -Filter "*.cs" |
    Select-String -Pattern "^\[Authorize" | Select-Object Path, Line
```

### Replacement Scripts Must Verify — MANDATORY
Every PowerShell string replacement MUST verify the change applied before proceeding to the next step or building. This is not optional. Pattern:
1. Detect line endings: `$le = if ($content -match "\`r\`n") { "\`r\`n" } else { "\`n" }`
2. Apply replacement using `$le` in match strings
3. Compare old and new content — if equal, write ERROR and exit 1
4. Only then proceed

Silent failures cause cascading bugs and wasted cycles. No exceptions.

### Full File Rewrites Over Regex Patching
For complex files, prefer full rewrites delivered as `.ps1` files over inline regex patching.

### Script Standards
- Name format: `Step{N}-{DayAbbrev}-{Description}.ps1` e.g. `Step12-Thur-SyncFileProgress.ps1`
- Every script starts with a header comment block listing all files changed
- Every script starts with `cls`
- Every script ends with the next required command (build or test) — no trailing prose
- Unicode characters (bullets etc.) must be built via `[char]0xNNNN` — never embed in here-strings
- Every replacement block must detect line endings before building match strings
- Every replacement block must verify the change applied before proceeding
- Scripts 50+ lines delivered as `.ps1` files via `present_files`
- Short blocks (<50 lines) pasted directly — must still include `cls`, `$le`, and verification

---

## BUGS - High Priority

All known bugs resolved. No open items.

---

## ROLE MIGRATION - ASP.NET Identity rollout

Cross-stage
- [Deferred] Documentation: dev guide on roles as canonical source
- [ ] Logging: failed authorization attempts
- [Deferred] Backfill/migration scripts and rollback notes

---

## SPRINT 2 — Reader Experience (Current)

- [DONE] CommentStatus enum implemented {New, AuthorReply, Ignore, Consider, Todo, Done, Keep}; SetStatus domain method and SetCommentStatus controller action in place
- [DONE] Author comment response UI — CommentStatus dropdown on Reader/Read (scene and chapter level, author/moderator only) and Author/Section; dashboard notifications link through to Author/Section
- [ ] Reader font preferences — font face and size selectable from Account/Settings page, persisted per reader, applied to reader view

## SPRINT 2.5 — Kindle-style Resume — Exact Scroll Position

Current state: resume redirects to correct scene via `#scene-{id}` anchor but does not restore exact scroll position within the scene. `ReadEvent` has no `ScrollPosition` field.

**Domain (TDD required)**
- [ ] Add `ScrollPosition` (nullable int) to `ReadEvent` entity
- [ ] Add `UpdateScrollPosition(int pixels)` domain method to `ReadEvent`
- [ ] Add domain tests: `UpdateScrollPosition_SetsScrollPosition`, `UpdateScrollPosition_OverwritesPreviousValue`

**Infrastructure**
- [ ] EF Core migration: add `ScrollPosition` column to `ReadEvents` table (nullable int)

**Application (TDD required)**
- [ ] Add `UpdateScrollPositionAsync(Guid sectionId, Guid userId, int scrollPosition)` to `IReadingProgressService`
- [ ] Stub with `NotImplementedException` in `ReadingProgressService`
- [ ] Add failing tests, then implement: finds existing `ReadEvent`, calls `UpdateScrollPosition`, saves
- [ ] Update `GetLastReadEventAsync` tests to cover `ScrollPosition` being returned

**Web — Controller**
- [ ] Add `[HttpPost] RecordScrollPosition(Guid sectionId, int scrollPosition)` to `ReaderController` — calls `UpdateScrollPositionAsync`, returns `Ok()`
- [ ] Update resume redirect in `DesktopDashboard` and `MobileDashboard` — use `?scrollTo={ScrollPosition}` instead of `#scene-` anchor if `ScrollPosition` is stored; fall back to `#scene-` anchor if null

**Web — JavaScript (DesktopRead.cshtml)**
- [ ] On scroll (debounced 500ms), POST `window.scrollY` to `RecordScrollPosition` for the currently visible scene
- [ ] On page load, if `?scrollTo=` query param present, call `window.scrollTo(0, scrollTo)` after short delay (replaces current `localStorage` restore)

**UAT**
- [ ] Read scene partway through, sign out, sign in — confirm returned to exact scroll position
- [ ] Confirm works across multiple projects (most recent across all)
- [ ] Confirm graceful fallback if section no longer published

---

## SPRINT 3 — Platform Hardening

- [ ] Fail2ban setup on production VM
- [ ] Report Fault modal — HomeController POST + _Layout.cshtml modal + CSS
- [ ] SystemStateMessage expiry — add ExpiresAt nullable DateTime, GetActiveAsync filters expired
- [ ] Add Project discovery flow (IScrivenerProjectDiscoveryService + Projects page UI)

---

## GO-LIVE GATE

These items are completed on the day of go-live, not before:
- [ ] Send password reset emails to Becca (becca@the-dunlops.co.uk) and Hilary (hilaryrrb@gmail.com)
- [ ] Confirm Becca and Hilary can log in and access The Fractured Lattice

---

# Post Go Live

## SPRINT 4 — Billing & Multi-tenancy

- [ ] IBillingProvider abstraction (Creem preferred, Paddle alternative)
- [ ] Subscription tiers: Free / Basic / Full (1 / 3 / unlimited active projects)
- [ ] ReaderTenant model (AuthorId, IsActive, IsDeleted, KnownAs)
- [ ] Account / TenancyMembership model (per v3 business model doc)
- [ ] Mark intentional single-tenancy seams for future refactor

---

## SHORT TERM - Go-Live Requirements

### Reader UX
- Kindle-style resume exact scroll position (→ Sprint 2)
- Reader notification emails (new chapter published)

### Dropbox Sync
- Dropbox OAuth2 token refresh — automatic refresh using stored refresh token (medium-term)
- Dropbox webhook controller for push-based sync (replace polling)
- Incremental sync — only download changed files (cursor-based, post-launch)

### Author Dashboard
- Show last download timestamp alongside last sync timestamp

### Config
- Audit remaining user secrets — anything not a password/token/key belongs in appsettings

### Mobile Author Views
- Readers page mobile: name, status, Deactivate only
- Author/Comments view: per chapter, all scenes comments, reply/delete inline
- Recent Activity: tap to open Author/Comments for that scene

### Author Chapter Page (Author/Chapter/{id})
- Full chapter view with all scenes
- Scene comments on RHS sidebar
- Chapter level comments in floating bar at bottom
- Reader progress: who has read it, date last visited
- Link from Sections list chapter rows

### Author Scene Page improvements
- Link back to parent chapter page
- "2 readers" hover showing which readers have read this scene

### Floating Footer Bar
- [Deferred → Sprint 3] Report Fault button → popup form

---

## MEDIUM TERM - Core Platform Features

### Publishing
- Part-level publish cascades all chapters + scenes below
- Book-level publish cascades everything below

### Dropbox
- Incremental sync: cursor-based change detection (only download changed files)
- Webhook controller for push-based sync (replace polling)
- In-app Dropbox re-auth page (deferred to go-live)

### BetaBooks Comment Importer
- Importer scoped to project name (prevents cross-project contamination)

### Add Project Discovery (→ Sprint 3)
- Add The Fractured Lattice as Books 2, 3 (UUIDs known)
- Dropbox vault scanning

---

## LONGER TERM - Business Model Features

### Multi-tenancy (→ Sprint 4)
- Account / TenancyMembership model (per v3 business model doc)
- Mark intentional single-tenancy seams for future refactor
- Concurrent per-tenant sync

### System Admin
- Prerequisite: tenancy model in place
- System Admin page — tenant list, connection status, reader count, disk/data size, tier
- SystemAdmin role attached to support@draftview.co.uk
- Tenant-level actions: suspend, unsuspend, view audit log

### ReaderTenant (Tenancy Phase)
- ReaderId, AuthorId, IsActive, IsDeleted, KnownAs, CreatedAt, DeactivatedAt, DeletedAt
- Deactivate = sets ReaderTenant.IsActive = false + revokes all ReaderAccess
- Bin/Delete = sets ReaderTenant.IsDeleted = true
- KnownAs = author's private nickname for this reader

### Reader Marketplace (Tenancy Phase)
- Reader can make themselves discoverable to other authors
- ReaderProfile entity: BragSheet (RTF), GenreList (many-to-many)
- Genre table seeded with common fiction genres

### Subscription / Billing (→ Sprint 4)
- Creem preferred (0% fee on first EUR1k/month)
- Paddle as alternative
- Backend-agnostic IBillingProvider abstraction
- Three tiers: Free, Basic, Full (1 / 3 / unlimited active projects)

### Standalone Sync Worker (DraftView.SyncWorker)
- Extract SyncBackgroundService into a separate worker service project
- Prerequisite: multi-tenancy model in place

### Scrivener Write-back (Phase 2)
- RTF annotations

---

## ARCHITECTURE - Phase 1-5 Review (stored, not started)

### Phase 1 - Stabilise single-tenancy
- Pass CancellationToken consistently in SyncService
- Validate section existence in ReadingProgressService.RecordOpenAsync
- Define and enforce active/inactive/soft-deleted user rules

### Phase 2 - Move mutations out of controllers
- ActivateProject, DeactivateProject, RemoveProject, AddProjects
- Create application services for each workflow
- Remove GetUnitOfWork(), GetCommentService(), GetReadEventRepo() service location
- Replace with constructor injection

### Phase 3 - Tighten application workflows
- Publication flow: enforce authorId or remove it
- Dashboard queries: move UI-shaped queries out of repositories

### Phase 4 - Make sync safer
- Replace controller Task.Run sync kickoff
- Check moved section handling in reconciliation
- Check reappearing soft-deleted section handling
- Add operational visibility: log discovery/parse failures

### Phase 5 - Prepare tenancy move
- Document single-tenancy seams: User.Role, GetAuthorAsync,
  GetAllBetaReadersAsync, GetReaderActiveProjectAsync,
  notification preferences scoping, comment visibility model

---

## CSS / FRONTEND

- CSS naming conventions refactor (BEM consistency)
- Remove duplicate .comment-box__reply-form declaration in Reader.css
- Replace hardcoded #f8f8f6 in .chapter-comment-form with var(--color-surface-alt)
- Replace hardcoded 15px in .chapter-comment-form__textarea with var(--text-base)

---

## DONE (this project)

### Bugs resolved
- [DONE] Reader/Read comment box overflows page boundary on RHS — fixed via CSS column width alignment and overflow guards (v2026-04-10-1)
- [DONE] AddComment POST redirects to top of chapter instead of returning to the commented scene — fixed in BaseReaderController.AddComment, now appends #scene-{id} anchor to redirect
- [DONE] AddComment POST on chapter-level comment box redirects to top of page instead of #chapter-comments — fixed in BaseReaderController.AddComment: when SectionId is a Folder node, anchor is set to #chapter-comments
- [DONE] SetCommentStatus POST on chapter-level comment redirects to a scene anchor instead of #chapter-comments — fixed in BaseReaderController.SetCommentStatus: when sceneId == chapterId, anchor is #chapter-comments
- [DONE] Reader/Read comment status dropdown missing — fixed in DesktopRead.cshtml; CommentStatus dropdown added for author/moderator on both scene-level and chapter-level comments
- [DONE] Author/Dashboard Recent Activity — replaced on-the-fly assembly with persisted AuthorNotification entity; truncation fixed, dismiss and clear all implemented, viewport clipping fixed
- [DONE] Login always redirected to Reader/Dashboard regardless of role — fixed; author lands on Author/Dashboard, SystemSupport on Support/Dashboard; post-success block wrapped in try/catch

### Sprint 1 — Pre-Beta Push (Complete)
- [DONE] Fix prose font in reader view — verified rendering correctly in production
- [DONE] Fix comment author display name — verified live lookup against AppUsers.DisplayName
- [DONE] Reactivate reader flow — UI and controller action both present and wired correctly

### Sprint 2 — Reader Experience (Partial, 2026-04-10)
- [DONE] Fix scene-level Published labels in sections view — verified working correctly
- [DONE] Fix Published Chapters sort order on dashboard — depth-first tree order (TDD)
- [DONE] Project switcher — sidebar in DesktopDashboard, query string selection, progress per project
- [DONE] Remember last selected project — query string naturally persists selection
- [DONE] Kindle-style resume on login — redirects to correct scene (exact scroll position deferred)
- [DONE] Persisted AuthorNotification entity — written at event time (comment, reply, reader joined, sync), dismiss single, clear all, 90-day auto-prune, viewport panel fix (404 tests green)
- [DONE] Login redirect fix — author → Author/Dashboard, SystemSupport → Support/Dashboard, try/catch on post-success block

### Persisted AuthorNotifications detail (2026-04-10, branch: feature/persisted-notifications)
- [DONE] AuthorNotification domain entity (TDD, 7 tests)
- [DONE] IAuthorNotificationRepository + EF implementation (8 tests)
- [DONE] EF migration AddAuthorNotifications (composite index on AuthorId, OccurredAt)
- [DONE] DashboardService.GetNotificationsAsync with 90-day prune, DismissNotificationAsync, DismissAllNotificationsAsync
- [DONE] CommentService writes NewComment / ReplyToAuthor notifications at event time
- [DONE] UserService writes ReaderJoined notification on AcceptInvitation
- [DONE] SyncService writes SyncCompleted notification on successful parse
- [DONE] AuthorController: Dashboard uses GetNotificationsAsync; DismissNotification + ClearAllNotifications POST actions added
- [DONE] Dashboard.cshtml: count badge, Clear All button, per-item dismiss button
- [DONE] DraftView.Notifications.css: dismiss button styles, viewport panel height fix
- [DONE] Removed obsolete: GetRecentNotificationsAsync, GetRecentCommentsForDashboardAsync, GetRecentlyAcceptedAsync, GetRecentlySyncedAsync, CommentNotificationRow
- [DONE] 404 tests GREEN

### Email Sprint (2026-04-08)
- [DONE] Yahoo SMTP for dev (smtp.mail.yahoo.com, port 587, app password)
- [DONE] SmtpEmailSender provider-agnostic via appsettings.json
- [DONE] Dev config: Yahoo SMTP via appsettings.Development.json (excluded from git)
- [DONE] Production config: Oracle Email Delivery SMTP via appsettings.Production.json
- [DONE] Oracle Email Delivery: DKIM active (draftview-prod selector), SPF updated, approved senders
- [DONE] Cloudflare DKIM CNAME added for draftview.co.uk
- [DONE] Cloudflare email routing: support@draftview.co.uk → alastair_clarke@yahoo.com
- [DONE] SmtpEmailSender migrated to MailKit 4.15.1 (fixes Oracle STARTTLS auth)
- [DONE] MimeKit vulnerability CVE-2026-30227 patched (4.7.1 → 4.15.1)
- [DONE] SQLite packages removed from solution
- [DONE] DraftView.Integration.Tests project added; SmtpEmailSenderIntegrationTests green
- [DONE] Test-OracleSmtp.ps1 helper script for Oracle SMTP credential testing
- [DONE] ForgotPassword SMTP failure caught and logged — no longer crashes page
- [DONE] Dev-safe email addresses for Becca and Hilary (becca.dev@draftview.local, hilary.dev@draftview.local)

### Role Migration — Stages 1-4 (2026-04-06)
- [DONE] Stage 1: Identity roles as canonical source, web surface migrated
- [DONE] Stage 2: IAuthorizationFacade injected into UserService
- [DONE] Stage 3: SystemSupport role, SystemStateMessage entity + service + footer integration
- [DONE] Stage 4: System State Message Management UI on Support Dashboard
- [DONE] ReaderController secured (Author,BetaReader); SystemSupport isolated
- [DONE] HomeController role-based routing (Support → Author → Reader)
- [DONE] CSS versioning automation (Update-CssVersion.ps1)
- [DONE] UserService.DeactivateUserAsync revokes all ReaderAccess records (TDD)
- [DONE] Mobile reader flow — IsMobile() view selection, MobileChapters/MobileScenes/MobileRead
- [DONE] Desktop reader views renamed to Desktop prefix

### Reader Authorization Model — FINAL DECISION
- Reader surface: Author + BetaReader only
- SystemSupport excluded from ReaderController; must use impersonation
- No dual-role model; Author treated as elevated reader at runtime
- BaseReaderController: `[Authorize(Roles = "Author,BetaReader")]`

### Impersonation — REQUIRED (NOT IMPLEMENTED)
- Read-only, explicit enter/exit mode
- Design agreed; not yet built

### Earlier Work
- [DONE] Step17-18: RtfConverter case-insensitive path fix, chapter ordering fix, email-as-nav-link
- [DONE] Step15-16: Account/Settings page; Dropbox panel for authors
- [DONE] Step15-16: User.UpdateDisplayName and UpdateEmail domain methods (TDD)
- [DONE] Step12-14: Sync file download progress — live file count, real percentage progress bar
- [DONE] LocalCachePath moved from user secrets to appsettings.json
- [DONE] Dual-list project assignment UI (ManageReaderAccess)
- [DONE] ReaderAccess entity + repository (TDD, migration)
- [DONE] Per-author Dropbox OAuth connection (DropboxConnection entity, IDropboxClientFactory)
- [DONE] IDropboxFileDownloader — full Dropbox sync working end to end
- [DONE] AuthorId added to ScrivenerProject (migration with backfill)
- [DONE] UseForwardedHeaders — fixes OAuth behind Nginx on production
- [DONE] Case-insensitive .scrivx lookup (Linux compatibility)
- [DONE] AddProjects background task (fixes 504 timeout)
- [DONE] BetaBooks importer: Comment.CreateForImport, DevTools command, 54 comments seeded
- [DONE] Becca Dunlop and Hilary Royston-Bishop accounts created with real emails
- [DONE] Toast notifications (fixed position, auto-dismiss, no layout shift)
- [DONE] Reply threading (roots + replies in reader and author views)
- [DONE] Comment edit and delete (including moderator delete)
- [DONE] PublishAsPartOfChapter domain invariant (TDD)
- [DONE] Chapter-only publishing enforced (domain + view)
- [DONE] CSS split into 7 files by concern
- [DONE] Heroicons integrated as static C# class
- [DONE] Rebrand: DraftReader → DraftView throughout
- [DONE] pg.ps1, PowerShell.md, PRINCIPLES.md
- [DONE] Production SMTP live (Oracle Email Delivery)
- [DONE] Cloudflare SSL, Nginx, systemd service on Oracle Cloud VM
- [DONE] Per-author Dropbox sync working end to end in production
- [DONE] Floating footer: copyright, system status indicator, Cloudflare email routing
- [DONE] BetaBooks importer: Comment.CreateForImport, 54 comments seeded for Becca and Hilary
- [DONE] Fix SmtpEmailSender From vs FromAddress (Phase 1 architecture)
- [DONE] Comment rules: authors and readers may reply (Phase 3 architecture decision)
