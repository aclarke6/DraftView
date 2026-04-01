# DraftView Task List
Last updated: 2026-04-01

---

## IMMEDIATE - Current Sprint

### ReaderAccess (In Progress)
- [DONE] ReaderAccess entity (TDD) — ReaderId, AuthorId, ProjectId, GrantedAt, RevokedAt
- [DONE] IReaderAccessRepository + ReaderAccessRepository
- [DONE] AddReaderAccess migration applied to dev
- Wire ReaderAccess into reader dashboard (filter projects by access)
- Assign readers to projects UI (on Add Project + Readers page)
- Invitation flow: existing account → skip to project assignment UI
- Auto-assign when author adds project (prompt on Add Project)

---

## SHORT TERM - Go-Live Requirements

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
- Incremental sync — only download changed files (cursor-based, post-launch)
- Dropbox webhook controller for push-based sync (replace polling)

### Author Dashboard - Sync Visibility
- Show cache file count per project on dashboard (visible sync health indicator)
- Show last download timestamp alongside last sync timestamp

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

### Fix prose font in reader view
- [PROBABLY ALREADY FIXED] Scrivener monospace overriding Georgia - needs verification

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

### Subscription / Billing
- Creem preferred (0% fee on first EUR1k/month)
- Paddle as alternative
- Backend-agnostic IBillingProvider abstraction
- Three tiers: Free, Paid, Ultimate

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

- [DONE] Per-author Dropbox OAuth connection (DropboxConnection entity, IDropboxClientFactory, OAuth flow)
- [DONE] IDropboxFileDownloader — full Dropbox sync working end to end
- [DONE] AuthorId added to ScrivenerProject (migration with backfill)
- [DONE] ReaderAccess entity + repository (TDD, migration)
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
- [DONE] 320 tests, all green