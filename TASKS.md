# DraftView — Task List
Last updated: 2026-04-19

---

## 0. Summary

**Live at:** https://draftview.co.uk
**Production:** Oracle Cloud VM `193.123.182.208`, .NET 10, PostgreSQL, Nginx, Cloudflare SSL
**Repository:** https://github.com/aclarke6/DraftView

### Current Test State
- **700 passing, 1 skipped, 0 failed** (latest full suite)
- 1 skipped — `SmtpEmailSenderIntegrationTests` (sends real email, manual only)

### Active Work
| Track | Status |
|-------|--------|
| V-Sprint 8 — Dropbox Incremental Sync | 🔄 In progress (Windows) |
| BugFix-Mac | 🟢 Synced with main, awaiting next bug |
| BugFix-PC | 🟢 Merged to main |

---

## 1. Reference Documents

| Document | Purpose |
|----------|---------|
| `Publishing And Versioning Architecture.md` | Full V-Sprint architecture, phases, domain model, publishing rules |
| `PRINCIPLES.md` | Core engineering principles |
| `REFACTORING.md` & `.github/instructions/versioning.instructions.md` | Refactoring rules and roadmap |
| `PowerShell.md` | PowerShell scripting standards |
| `DraftView Git Rules.md` | Branch strategy, merge gates, commit standards |
| `.github/copilot-instructions.md` | Agent instructions for Copilot/Claude sessions |
| `Sprint4-EmailPrivacy.md` | Email privacy sprint detail (Sprint 4, all phases complete) |
| `Sprints-Legacy.md` | Sprints 1–3, Role Migration, Font Preferences, ScrivenerProject rename (all complete) |

### Prompt Files (by sprint)
- `vsprint1-phase1.prompt.md` through `vsprint1-phase6.prompt.md`
- `vsprint2-phase1.prompt.md` through `vsprint2-phase3.prompt.md`
- `vsprint3-phase2.prompt.md`, `vsprint3-phase3.prompt.md`
- `vsprint4-phase1.prompt.md` through `vsprint4-phase3.prompt.md`
- `vsprint5-phase1.prompt.md` through `vsprint5-phase3.prompt.md`
- `vsprint6-phase1.prompt.md`, `vsprint6-phase2.prompt.md`
- `vsprint7-phase1.prompt.md`, `vsprint7-phase2.prompt.md`
- `vsprint8-phase1.prompt.md`
- `bugfix-diff-para-removed.prompt.md`
- `bugfix-mailkit-nu1902.prompt.md`
- `BugFixes/invite-reader-submit-production-crash.md`
- `BugFixes/mac-cross-platform-local-cache-path.md`
- `production-reset.prompt.md`

---

## 2. Open Bugs

- [Open] **Removing a reader from `/Author/Readers` does not remove the reader from the list**
  - action completes but reader remains visible
  - investigate: `AuthorController.SoftDeleteReader`, `SoftDeleteUserAsync`, reader list filter

- [Open] **System Support has no readers page**
  - no UI surface to verify deny-by-default email behaviour for SystemSupport role
  - decide: dedicated support readers screen, or remove from UAT scope

- [Open] **Reader settings shows `Ciphertext is not in the expected format` on screen**
  - protected-email decryption failure surfacing as a form validation error
  - should route through controlled 500 error path
  - investigate: `AccountController` settings actions, production rows with invalid `EmailCiphertext`

---

## 3. Active Projects

### 3.1 V-Sprint Series — Publishing and Versioning
See `Publishing And Versioning Architecture.md` for full architecture, domain model, and sprint specifications.

- [x] V-Sprint 1 — Core versioning backbone + manual upload — **COMPLETE**
- [x] V-Sprint 2 — Paragraph diff highlighting — **COMPLETE**
- [x] V-Sprint 3 — Reader experience layer (state, messaging, banner) — **COMPLETE**
- [x] V-Sprint 4 — Pending change indicator and classification — **COMPLETE**
- [x] V-Sprint 5 — AI summary service and reader banner — **COMPLETE**
- [x] V-Sprint 6 — Per-document publishing and Publishing Page — **COMPLETE**
- [x] V-Sprint 7 — Chapter locking and scheduling — **COMPLETE**
- [x] **V-Sprint 8 — Dropbox incremental sync** 🔄
  - [x] Phase 1 — Cursor-based incremental sync (`Project.DropboxCursor`, `ListChangedEntriesAsync`, `ListAllEntriesWithCursorAsync`, full sync on first run, cursor-expired fallback, deleted entries soft-deleted)
- [x] V-Sprint 9 — Version retention and deletion
  - [x] Phase 1 — Retention domain (rules per pricing tier, physical deletion)
  - [x] Phase 2 — Enforcement (limit check before version creation)
  - [x] Phase 3 — Version management UI (version list on Publishing Page)
- [x] V-Sprint 10 — Tree builder UI (Option A, post-launch)
  - [x] Phase 1 — Tree service extension
  - [x] Phase 2 — Tree builder UI
  - [x] Phase 3 — Sync project tree display

### 3.2 Platform Hardening
- [ ] Fail2ban setup on production VM
- [ ] Report Fault modal (HomeController POST + `_Layout.cshtml` modal + CSS)
- [ ] SystemStateMessage expiry (`ExpiresAt` nullable DateTime, `GetActiveAsync` filters expired)
- [ ] Logging: failed authorization attempts
- [ ] Impersonation — read-only, explicit enter/exit mode (design agreed, not built)

### 3.3 Kindle-style Resume — Exact Scroll Position
- [ ] `ScrollPosition` (nullable int) on `ReadEvent`, `UpdateScrollPosition` domain method
- [ ] EF migration: `ScrollPosition` column on `ReadEvents`
- [ ] `UpdateScrollPositionAsync` on `IReadingProgressService`
- [ ] `RecordScrollPosition` POST action on `ReaderController`
- [ ] Resume redirect uses `?scrollTo=` query param if stored
- [ ] JS: debounced scroll POST, page-load restore

### 3.4 Billing and Multi-tenancy (Post Go-Live)
See `ScrivenerSync-BillingModel-v2.docx` and `ScrivenerSync-BusinessModel-v3.docx`.
- [ ] `IBillingProvider` abstraction (Creem preferred, Paddle alternative)
- [ ] Subscription tiers: Free / Paid / Ultimate
- [ ] `ReaderTenant` model (`AuthorId`, `IsActive`, `IsDeleted`, `KnownAs`)
- [ ] Reader Marketplace (post-revenue)

### 3.5 Incremental Refactor Roadmap
See `REFACTORING.md` for full detail.
- [x] Phase 1 — Centralise controller user/role resolution — **COMPLETE**
- [ ] Phase 2 — Extract procedural controller workflows (UpdateReaderAccess, Section query, AddProjects)
- [ ] Phase 3 — Decompose startup/seeding
- [ ] Phase 4 — Standardise inheritance and shared utilities
- [ ] Phase 5 — Extract remaining procedural workflows
- [ ] Phase 6 — Standardise sync kickoff (remove inline `Task.Run`)

---

## 4. Go-Live Prerequisites

### Must Complete Before Go-Live
- [ ] Add `Anthropic:ApiKey` to `appsettings.Production.json` on the production server (enables AI summaries)
- [ ] Desktop: `/Author/InviteReader` sends invitation with correct production `App:BaseUrl`
- [ ] Desktop: invitation acceptance flow does not expose stored email
- [ ] Desktop: forgot-password flow works end-to-end in production
- [ ] Desktop: support flows do not reveal user email
- [ ] Mobile: account settings is the only self-service email view
- [ ] Production smoke check: no `localhost` links, no plaintext email leakage, no fake-success on operational failures
- [ ] Data handling aligns with UK GDPR and Data Protection Act 2018

### Key Management
- [ ] Copy production `EmailProtection:EncryptionKey` and `EmailProtection:LookupHmacKey` into a secure password manager
- [ ] Consider moving keys to systemd environment variables (removes secrets from web root)
- [ ] Confirm a second person or secure location holds recovery keys
- [ ] Document recovery procedure: lost keys = unrecoverable encrypted emails

### Go-Live Day (run on the day, not before)
- [ ] Send password reset emails to Becca (becca@the-dunlops.co.uk) and Hilary (hilaryrrb@gmail.com)
- [ ] Confirm Becca and Hilary can log in and access The Fractured Lattice

### Backlog (post go-live)
- Reader notification emails (new chapter published)
- Show last download timestamp alongside last sync timestamp
- Dropbox OAuth2 token refresh
- Dropbox webhook controller for push-based sync
- In-app Dropbox re-auth page
- Author/Comments view (mobile)
- Author Chapter Page (`Author/Chapter/{id}`)
- Publishing cascades (part-level, book-level)
- Audit remaining user secrets

---

## 5. Done

### 5a. Bugs Fixed
- [DONE] Cross-platform local cache path resolution — `IPlatformPathService`, `PlatformPathService`, platform-aware fallback in `LocalPathResolver`, `appsettings.json` `LocalCachePath` cleared (BugFix-Mac, 2026-04-19)
- [DONE] `/Author/InviteReader` submit production crash — operational failures now route to `Home/Error`; `InvariantViolationException` remains a form validation response (BugFix-Mac, 2026-04-19)
- [DONE] MailKit NU1902 vulnerability — upgraded to 4.16.0 (BugFix-PC, 2026-04-19)
- [DONE] Reader view does not apply saved Reading Preferences — resolved 2026-04-17
- [DONE] CS9107 in `AccountController` primary constructor — resolved 2026-04-17
- [DONE] Reader/Read mobile view 404 — resolved
- [DONE] Reader/Read comment box overflows page boundary on RHS — CSS fix
- [DONE] AddComment POST redirects to top of chapter/page — fixed with `#scene-{id}` / `#chapter-comments` anchors
- [DONE] SetCommentStatus POST redirects to wrong anchor — fixed
- [DONE] Reader/Read comment status dropdown missing — added for author/moderator
- [DONE] Author/Dashboard Recent Activity truncation — replaced with persisted `AuthorNotification`
- [DONE] Login always redirected to Reader/Dashboard — fixed role-based redirect
- [DONE] Reader diff UX for removed paragraphs — thin markers instead of strikethrough
- [DONE] Removed paragraphs diff issue — resolved (bugfix-diff-para-removed)

### 5b. Sprints and Projects Complete
- [DONE] **V-Sprints 1–7** — see `Publishing And Versioning Architecture.md`
- [DONE] **Sprint 4 — Email Privacy and Controlled Access** — see `Sprint4-EmailPrivacy.md`
- [DONE] **Sprint 3 — Reader Font Preferences** — `ProseFont`, `ProseFontSize` on `UserPreferences`, Google Fonts, CSS variables
- [DONE] **Sprint 2 — Reader Experience** — project switcher, Kindle-style resume (anchor), AuthorNotifications, CommentStatus
- [DONE] **Sprint 1 — Pre-Beta Push** — prose font, comment display name, reader reactivation
- [DONE] **Email Sprint** — Oracle Email Delivery SMTP, MailKit, DKIM, SPF, Cloudflare routing
- [DONE] **Role Migration** — Identity roles, SystemSupport, SystemStateMessage, mobile reader flow
- [DONE] **ScrivenerProject → Project rename** — full solution-wide rename, SyncRootId, migration
- [DONE] **UserNotificationPreferences → UserPreferences rename** — migration, DI, theme toggle
- [DONE] **Incremental Refactor Phase 1** — `BaseController` auth helpers, controller guard consolidation

### 5c. Other Completed
- [DONE] Audited `Views/Author/Publishing.cshtml` for style leakage during V-Sprint 9 Phase 3 version-management UI; retained existing inline form display pattern only and moved version-history styling into `DraftView.Core.css`
- [DONE] Audited `Views/Author/Sections.cshtml` for style leakage during V-Sprint 10 Phase 2 tree builder UI; retained existing inline form pattern only and moved tree-builder interaction styling into `DraftView.Core.css`
- [DONE] Audited `Views/Author/Sections.cshtml` for style leakage during V-Sprint 10 Phase 3 sync tree display; retained existing inline table pattern and added sync-specific read-only/badge styles in `DraftView.Core.css`
- [DONE] Production infrastructure — Oracle Cloud VM, Nginx, Cloudflare SSL, systemd service
- [DONE] `IDropboxFileDownloader` — full Dropbox sync end to end in production
- [DONE] `publish-draftview.ps1` deploy script
- [DONE] `pg.ps1` PostgreSQL helper
- [DONE] `run-query.sh` production bash query helper
- [DONE] CSS split into 7 files by concern; Heroicons as static C# class
- [DONE] Rebrand: DraftReader → DraftView
- [DONE] BetaBooks comment importer (54 comments seeded)
- [DONE] Becca Dunlop and Hilary Royston-Bishop accounts with real emails
- [DONE] Toast notifications
- [DONE] Production database rebuild (2026-04-19) — SectionVersions truncated, sections unpublished, ReadEvents cleared, sync and republish to follow
