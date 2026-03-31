# DraftView Task List
Last updated: 2026-03-31

---

## IMMEDIATE - Current Sprint

### Phase 1 Defect Fixes
- [DONE] 1A - Persist accepted reader display name
- [DONE] 1B - Invitation email includes expiry info (never-expiring or expiry date)
- [DONE] 1C - Readers list status wording (Invited / Active / Inactive, no "Pending")
- [DONE] 1D - Invite email recipient name uses email local part

---

## SHORT TERM - Go-Live Requirements

### Mobile Author Views
- Simplified mobile Sections view: chapters only, no scene rows, Publish/Unpublish only
- Author/Comments view: per chapter, all scenes comments, reply/delete inline
- Recent Activity: tap to open Author/Comments for that scene
- Readers page mobile: name, status, Deactivate only
- Must have on mobile: view comments, reply, moderator delete, invite reader, deactivate reader
- Desktop only: sync controls, project management, add/remove projects

### Author Chapter Page (Author/Chapter/{id})
- Full chapter view with all scenes
- Scene comments on RHS sidebar
- Chapter level comments in floating bar at bottom
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
- Wire up Oracle Email Delivery into IEmailSender (currently Console only)
- Cloudflare email routing: support@draftview.co.uk -> alastair_clarke@yahoo.com

### Fix prose font in reader view
- Scrivener monospace overriding Georgia

---

## MEDIUM TERM - Core Platform Features

### Publishing (PARTIALLY DONE)
- [DONE] Chapter-only publishing in Sections view
- [DONE] Parts/Books excluded from Publish button
- [DONE] Domain invariant: PublishAsPartOfChapter enforced
- Part-level publish cascades all chapters + scenes below
- Book-level publish cascades everything below

### Reader Flow
- Reactivate reader flow UI (exists but needs wiring)
- Reader notification emails (new chapter published)
- CSS for reader view: iOS, Android, phones, tablets, full screens

### Dropbox
- Webhook controller for push-based sync (replace polling)
- Dropbox OAuth2 token refresh (eliminate manual re-auth)
- In-app Dropbox re-auth page

### SMTP Email for Production
- Currently Console only
- Fix SmtpEmailSender using FromAddress not From

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

### BetaBooks Comment Importer
- Create Account records, issue invitations
- Seed historical comments from betabooks-export.json
- Match by chapter number and reader name

### Add Project Discovery
- IScrivenerProjectDiscoveryService
- Projects page UI: discover and add without manual DB work
- Add The Fractured Lattice as Books 1, 2, 3 (UUIDs known)
- Dropbox vault scanning

### Scrivener Write-back (Phase 2)
- RTF annotations

### GitHub Repository Rename
- ScrivenerSync -> DraftView (pending)

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

- [DONE] 1A - Persist accepted reader display name
- [DONE] 1B - Invitation email expiry info + recipient name (1D combined)
- [DONE] 1C - Readers list status wording (Invited / Active / Inactive)
- [DONE] CommentStatus enum (New, AuthorReply, Ignore, Consider, Todo, Done, Keep)
- [DONE] Notifications panel on author dashboard
- [DONE] Comment author display names
- [DONE] Reply threading (roots + replies in reader and author views)
- [DONE] Heroicons (Edit, Delete, Reply, Send, Save, Cancel)
- [DONE] Read.cshtml migrated to CSS classes, no inline styles
- [DONE] CSS split into 7 files by concern
- [DONE] Mobile CSS for reader and author views
- [DONE] Chapter-only publishing enforced (domain + view)
- [DONE] PublishAsPartOfChapter domain invariant (TDD)
- [DONE] Orphaned published scenes cleaned from DB
- [DONE] Reader dashboard excludes Part/Book folders
- [DONE] Sections view: Published badge suppressed on scene rows
- [DONE] Comment edit and delete (including moderator delete)
- [DONE] pg.ps1 helper script committed to git
- [DONE] 285 tests, all green
