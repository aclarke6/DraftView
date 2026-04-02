# DraftView Task List
Last updated: 2026-04-02

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

### TDD Required for Domain Entities
All new domain entities require tests before implementation. No exceptions.

### CSS Version — MANDATORY on Every CSS Change
Every script that modifies any `.css` file must also bump `--css-version` in `DraftView.Core.css`. Format: `v{YYYY}-{MM}-{DD}-{n}` where n increments if multiple changes on the same day. Never skip this step.

Always use regex replace so it matches whatever version is currently there — never hardcode the expected current value:
```powershell
$core = $core -replace '--css-version: "v[^"]+";', '--css-version: "v2026-04-02-1";'
if ($core -notmatch 'v2026-04-02-1') { Write-Host "ERROR: bump failed" -ForegroundColor Red; exit 1 }
```

### Controller Action Guards — MANDATORY
Every public action in `AuthorController` must call `RequireAuthorAsync()` or `GetAuthorAsync()` as the first statement. No exceptions. Before adding any new action, verify the guard is present. When auditing, use:
```
Select-String -Path "AuthorController.cs" -Pattern "public async Task<IActionResult>|GetAuthorAsync|RequireAuthorAsync"
```
and confirm every action has a guard on the following line.

### Replacement Scripts Must Verify — MANDATORY
Every PowerShell string replacement MUST verify the change applied before proceeding to the next step or building. This is not optional. Pattern:
1. Apply replacement
2. Compare old and new content — if equal, write ERROR and exit 1
3. Only then proceed

Silent failures cause cascading bugs and wasted cycles. No exceptions.

### Full File Rewrites Over Regex Patching
For complex files, prefer full rewrites delivered as `.ps1` files over inline regex patching.

### Script Standards
- Name format: `Step{N}-{DayAbbrev}-{Description}.ps1` e.g. `Step12-Thur-SyncFileProgress.ps1`
- Every script starts with a header comment block listing all files changed
- Unicode characters (bullets etc.) must be built via `[char]0xNNNN` — never embed in here-strings

---

## BUGS - High Priority

### Scene Status Not Updating on Sync
- **Reported:** 2026-04-02, production
- **Status:** RESOLVED — not a code defect. Scrivener defers metadata writes until explicit save. Author must save in Scrivener before triggering a DraftView sync for status changes to be picked up. Code is correct end-to-end.
- **Workflow note:** Document in author help: always save in Scrivener (Ctrl+S) before syncing in DraftView.

---

## IMMEDIATE - Current Sprint

### Account/Settings Page (Both Roles)
- **Why:** Authors have no way to reach Dropbox settings from the nav. Readers have an Account link but the page doesn't exist. Both roles need account management in one place.
- Add "Account" nav link for authors in `_Layout.cshtml` (currently only shown to readers)
- `Account/Settings` renders differently by role — shared fields at top, role-specific panel below
- **Shared (Author + Reader):**
  - Display name change
  - Password change (inline form using current password as verification — not email reset)
  - Email change
- **Author only (below account fields):**
  - Dropbox connection panel: shows Connected/Disconnected status, Connect/Reconnect/Disconnect button
  - Replaces orphaned `/Dropbox/Settings` as the author entry point for Dropbox management
  - `/Dropbox/Settings` controller actions remain — `Account/Settings` links through to them
- **Reader only:**
  - No additional fields at this stage
  - BragSheet and Genre preferences are deferred to Reader Marketplace (tenancy phase)
- **Note:** The existing ForgotPassword/ResetPassword email flow on the login page covers unauthenticated reset. `Account/Settings` provides authenticated password change only.

### ReaderAccess (In Progress)
- [DONE] ReaderAccess entity (TDD) — ReaderId, AuthorId, ProjectId, GrantedAt, RevokedAt
- [DONE] IReaderAccessRepository + ReaderAccessRepository
- [DONE] AddReaderAccess migration applied to dev
- [DONE] Reader dashboard filters projects by ReaderAccess
- [DONE] Author Reader View bypasses ReaderAccess — sees all active projects
- [DONE] Multiple active projects per author (removed single-active constraint)
- [DONE] ReaderController.Read uses chapter.ProjectId (fixes multi-project prose display)
- [DONE] Dual-list project assignment UI (Author/ManageReaderAccess)
- [DONE] Readers list redesign — name as link, icon buttons (NoSymbol, Restore, Delete)
- [DONE] AuthorController access control — RequireAuthorAsync guard on all actions
- [HELD] Fix Reader/Read LHS sidebar — position:sticky not working, parent container issue
- [HELD] Reader Dashboard layout redesign — LHS sticky book list, RHS chapter list for selected book
- Invitation flow: existing account → skip to project assignment UI
- Auto-assign when author adds project — prompt to assign existing readers
- Fix Deactivate to also revoke all ReaderAccess records for this author

---

## SHORT TERM - Go-Live Requirements

### Reader UX
- Project switcher for readers/authors with multiple projects
  - Dropdown or tab strip when >1 project accessible
  - Remember last selected project (cookie or session)
- Kindle-style resume on login
  - Redirect reader to last chapter/scene they were reading
  - Derive from most recent ReadEvent for the reader

### Dropbox Sync (DONE - production live)
- [DONE] Per-author DropboxConnection entity (TDD)
- [DONE] OAuth2 connect/callback/disconnect flow (DropboxController)
- [DONE] IDropboxClientFactory — per-author token, auto-refresh
- [DONE] IDropboxFileDownloader — downloads .scriv folder to per-author cache
- [DONE] SyncService scoped to project AuthorId
- [DONE] LocalPathResolver per-author cache path ({cachePath}/{userId}/)
- [DONE] ScrivenerProjectDiscoveryService uses IDropboxClientFactory
- [DONE] AddAuthorId migration (with backfill)
- [DONE] AddReaderAccess migration
- [DONE] UseForwardedHeaders (fixes OAuth behind Nginx)
- [DONE] Case-insensitive .scrivx file lookup (Linux fix)
- [DONE] AddProjects fires sync as background task (fixes 504)
- Dropbox OAuth2 token refresh — automatic refresh using stored refresh token (medium-term)
- Dropbox webhook controller for push-based sync (replace polling)
- Incremental sync — only download changed files (cursor-based, post-launch)

### Author Dashboard - Sync Visibility
- [DONE] Progress bar during sync
- [DONE] Live file download count: "Downloading... 180 / 1771 files" (Steps 12-14)
- [DONE] Real progress bar driven by filesDownloaded/totalFiles percentage (Step 14)
- Show cache file count per project on dashboard (visible sync health indicator)
- Show last download timestamp alongside last sync timestamp

### Config: Move Non-Secret Settings Out of User Secrets
- [DONE] `LocalCachePath` moved to `appsettings.json` (was incorrectly in user secrets)
- Audit remaining user secrets — anything not a password/token/key belongs in appsettings

### Mobile Author Views
- Readers page mobile: name, status, Deactivate only (table scrolls - acceptable)
- Author/Comments view: per chapter, all scenes comments, reply/delete inline
- Recent Activity: tap to open Author/Comments for that scene

### Author Chapter Page (Author/Chapter/{id})
- Full chapter view with all scenes
- Scene comments on RHS sidebar
- Chapter level comments in floating bar at bottom (BetaBooks comments land here)
- Reader progress: who has read it, date last visited
- Link from Sections list chapter rows

### Author Scene Page improvements
- Link back to parent chapter page
- "2 readers" hover showing which readers have read this scene

### Floating Footer Bar (both reader and author views)
- Copyright details
- System status / outages indicator
- Report Fault button -> popup form (not full page)
  - Pre-populate user email and name from DB
  - Subject + comment box + Send/Cancel
  - Emails support@draftview.co.uk -> alastair_clarke@yahoo.com
- Cloudflare email routing setup for support@draftview.co.uk

### Email Infrastructure
- Wire up SMTP (Resend) into IEmailSender (currently Console only)
- Cloudflare email routing: support@draftview.co.uk -> alastair_clarke@yahoo.com

### Reader Flow
- Reactivate reader flow UI (exists but needs wiring)
- Reader notification emails (new chapter published)

### Production - Pre-Beta Push
- Fix prose font in reader view (verify after sync)
- Reactivate reader flow
- Wire up SMTP email
- Send password reset emails to becca@the-dunlops.co.uk and hilaryrrb@gmail.com
- Fail2ban setup on production VM

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
- [DONE] Comment.CreateForImport domain factory (TDD)
- [DONE] BetaBooksImporter DevTools command
- [DONE] 54 comments seeded for Becca and Hilary
- [DONE] Reader accounts created with real emails
- Importer scoped to project name (prevents cross-project contamination)

### Add Project Discovery
- Add The Fractured Lattice as Books 2, 3 (UUIDs known)
- Dropbox vault scanning

---

## LONGER TERM - Business Model Features

### Multi-tenancy
- Account / TenancyMembership model (per v3 business model doc)
- Mark intentional single-tenancy seams for future refactor
- Concurrent per-tenant sync — each tenant's sync runs independently; SyncWorker uses one Task per tenant with isolated failure handling

### System Admin
- Prerequisite: tenancy model in place
- System Admin page — tenant list, connection status, reader count, disk/data size, tier (Free/Basic/Unlimited)
- SystemAdmin role attached to support@draftview.co.uk
- Tenant-level actions: suspend, unsuspend, view audit log

### ReaderTenant (Tenancy Phase)
- New table scoping reader state per author/tenant:
  - ReaderId, AuthorId, IsActive, IsDeleted, KnownAs, CreatedAt, DeactivatedAt, DeletedAt
- Deactivate = sets ReaderTenant.IsActive = false + revokes all ReaderAccess (tenant-scoped)
- Bin/Delete = sets ReaderTenant.IsDeleted = true (hidden from author's Readers list)
- KnownAs = author's private nickname for this reader
- Replaces current User.IsActive / User.IsSoftDeleted for tenant-scoped operations

### Reader Marketplace (Tenancy Phase)
- Reader can make themselves discoverable to other authors
- ReaderProfile entity:
  - BragSheet (RTF field) — reader's self-description as a beta reader
  - GenreList (many-to-many) — genres they enjoy beta reading
- Genre table seeded with common fiction genres
- Author can browse available readers and invite directly
- Reader Account/Settings page gains BragSheet and Genre fields at this point

### Subscription / Billing
- Creem preferred (0% fee on first EUR1k/month)
- Paddle as alternative
- Backend-agnostic IBillingProvider abstraction
- Three tiers: Free, Basic, Full (1 / 3 / unlimited active projects)

### Standalone Sync Worker (DraftView.SyncWorker)
- Extract SyncBackgroundService into a separate worker service project
- Shares DraftView.Application and DraftView.Infrastructure
- Cycles through all tenants' projects independently of web app restarts
- Deployable as a separate process or cheap VM
- Prerequisite: multi-tenancy model in place
- Design note: current interfaces are already clean enough for extraction

### Scrivener Write-back (Phase 2)
- RTF annotations

---

## ARCHITECTURE - Phase 1-5 Review (stored, not started)

### Phase 1 - Stabilise single-tenancy
- Fix SmtpEmailSender From vs FromAddress
- Remove sync-over-async from RedirectToLocal
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
- Comment rules: authors and readers may reply (decided)
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
- Bump CSS version in Core.css and _Layout.cshtml on every CSS change

---

## DONE (this project)

- [DONE] Step12-14: Sync file download progress — live file count, total files, real percentage progress bar
- [DONE] LocalCachePath moved from user secrets to appsettings.json
- [DONE] Scene status sync confirmed working — Scrivener save timing documented
- [DONE] Dual-list project assignment UI (ManageReaderAccess)
- [DONE] ReaderAccess entity + repository (TDD, migration)
- [DONE] Reader dashboard filters by ReaderAccess per reader
- [DONE] Author Reader View shows all active projects (bypasses ReaderAccess)
- [DONE] Multiple active projects per author
- [DONE] ReaderController.Read fixed for multi-project (uses chapter.ProjectId)
- [DONE] Per-author Dropbox OAuth connection (DropboxConnection entity, IDropboxClientFactory, OAuth flow)
- [DONE] IDropboxFileDownloader — full Dropbox sync working end to end
- [DONE] AuthorId added to ScrivenerProject (migration with backfill)
- [DONE] UseForwardedHeaders — fixes OAuth behind Nginx on production
- [DONE] Case-insensitive .scrivx lookup (Linux compatibility)
- [DONE] AddProjects background task (fixes 504 timeout)
- [DONE] 1A - Persist accepted reader display name
- [DONE] 1B - Invitation email expiry info + recipient name (1D combined)
- [DONE] 1C - Readers list status wording (Invited / Active / Inactive)
- [DONE] BetaBooks importer: Comment.CreateForImport, DevTools command, 54 comments seeded
- [DONE] Becca Dunlop and Hilary Royston-Bishop accounts created with real emails
- [DONE] Sections view: scroll to chapter after publish/unpublish
- [DONE] Toast notifications (fixed position, auto-dismiss, no layout shift)
- [DONE] Mobile author views: scene rows hidden, desktop-only controls, projects table fix
- [DONE] SyncService: ILogger added, errors logged to console
- [DONE] Rebrand: DraftReader -> DraftView throughout
- [DONE] CommentStatus enum
- [DONE] Notifications panel on author dashboard
- [DONE] Comment author display names
- [DONE] Reply threading (roots + replies in reader and author views)
- [DONE] Heroicons
- [DONE] Read.cshtml migrated to CSS classes
- [DONE] CSS split into 7 files by concern
- [DONE] Mobile CSS for reader and author views
- [DONE] Chapter-only publishing enforced (domain + view)
- [DONE] PublishAsPartOfChapter domain invariant (TDD)
- [DONE] Orphaned published scenes cleaned from DB
- [DONE] Reader dashboard excludes Part/Book folders
- [DONE] Sections view: Published badge suppressed on scene rows
- [DONE] Comment edit and delete (including moderator delete)
- [DONE] pg.ps1 helper script
- [DONE] PowerShell.md scripting standards document
- [DONE] PRINCIPLES.md scripting standards document
- [DONE] 320 tests, all green