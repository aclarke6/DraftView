# DraftView — Task List
Last updated: 2026-08-17
Last deployed: 2026-08-17 13:59 (commit: d51d201)

---

## 0. Summary

**Live at:** https://draftview.co.uk
**Production:** Oracle Cloud VM `141.147.71.62`, .NET 10, PostgreSQL, Nginx, Cloudflare SSL
**Repository:** https://github.com/aclarke6/DraftView

### Current Test State
- 862 total, 862 passed, 1 skipped, 0 failed
- 1 skipped — `SmtpEmailSenderIntegrationTests` (sends real email, manual only)

### Active Work
| Track | Status |
|-------|--------|
| RSprint Series | 🟡 In progress — RS-A to RS-E complete, RS-F next |
| S-Sprint Series | 🟡 In progress — S-Sprint-1 complete, S-Sprint-2 next |
| MT-Sprint Series | 🔴 HIGH PRIORITY — second author interest accelerates timeline; see `MultiTenancy.md` |
| RD-Sprint Series | 🟡 In progress — RD-Sprint-1 (Continue Reading CTA) merged to main 2026-08-17; see section 3.7 |
| DR-Sprint Series | ✅ Complete — merged to main 2026-08-17; see section 3.8 |
| Go-Live Prerequisites | 🔴 Blocking — items below must complete before launch |
| UAT | 🟡 In progress |

---

## 1. Reference Documents

| Document | Purpose |
|----------|---------|
| `AGENTS.md` | Authoritative execution rules for all coding agents — defines constraints, architecture boundaries, TDD requirements, and hard-gated response behaviour across all tools |
| `Passage Anchoring, Reader Continuity, and Inline Commentary.md` | Authoritative design for passage anchoring, relocation, reader continuity, and inline commentary (RSprint series) |
| `DropBox Synchronisation Using WebHooks.md` | Webhook-driven background Dropbox sync — control model, cursor-based interrogation, and S-Sprint series |
| `MultiTenancy.md` | Multi-tenancy sprint series, design decisions, and migration strategy |
| `Publishing And Versioning Architecture.md` | Versioning model — SectionVersion, publish/republish rules, and lifecycle behaviour |
| `DraftView-UAT-Plan.md` | UAT plan and validation scenarios for reader and author workflows |
| `PRINCIPLES.md` | Core engineering principles — architecture, layering, and behavioural rules |
| `REFACTORING.md` | Refactoring roadmap and constraints for safe structural improvement |
| `PowerShell.md` | PowerShell scripting standards for safe file modification and verification |
| `DraftView Git Rules.md` | Branching strategy, merge gates, and commit standards |
| `.github/copilot-instructions.md` | Supplemental agent guidance for repository-integrated coding agents |
| `HISTORY.md` | Completed bugs, changes, sprints, and phases |

---

## 2. Open Minor Work

### 2(a) Bugs
No open bugs.

### 2(b) Changes

- [SUPERSEDED] CHANGE-003 — replaced by CHANGE-006 and CHANGE-007 below

- [ ] CHANGE-006 — Reader left nav: collapsible act tree + panel pin/unpin

  **Goal:** Replace the static "Other Chapters" flat list with a collapsible act tree, and make the whole left panel pin/unpin like OneNote.

  **Nav tree structure:**
  - Book title shown as the panel header (unchanged)
  - Each intermediate folder (Act, Part, or whatever the author named it) becomes a collapsible row, labelled with the author's own title, with a `›` chevron toggle
  - The act containing the current chapter starts expanded; all others start collapsed
  - "Other Chapters" heading and section are removed — all chapter navigation lives inside the act tree
  - "This Chapter" scene list remains at the top, unchanged

  **Data model — no changes needed:**
  `ContentGroup.Heading` already carries the author's folder title. `BuildContentGroups` already produces the recursive `SubGroups` structure. The view just needs to render these as toggles instead of static headings.

  **Panel pin/unpin behaviour:**
  - Desktop (≥ 992 px): panel starts pinned open
  - Mobile/tablet: panel starts collapsed; a `☰` button opens it as an overlay
  - Unpinned: panel auto-collapses after a chapter link is clicked
  - Pinned: panel stays open after selection
  - A pin icon (📌 / thumb-tack SVG) toggles pin state
  - Pin state and per-act collapsed state persisted in `localStorage`

  **Files affected:**
  - `DraftView.Web/Views/Reader/DesktopRead.cshtml` — replace static headings with `<button>` toggles; add pin button; remove "Other Chapters" block
  - `DraftView.Web/wwwroot/css/DraftView.Reader.css` — panel slide transition, collapsed state, pin icon, overlay backdrop
  - `DraftView.Web/wwwroot/js/reader-nav.js` *(new)* — toggle logic, pin state, localStorage, auto-collapse on nav

- [DONE] CHANGE-006 + CHANGE-007 — Collapsible reader nav and comments toggle — merged to main 2026-08-15. See `HISTORY.md`.

- [x] CHANGE-008 — Mobile reader: chapter comments page + disable passage-anchor capture on touch — merged 2026-08-16. See `HISTORY.md`.
- [x] CHANGE-009 — Mobile reader: read-first scene comments — merged 2026-08-16. See `HISTORY.md`.

---

## 3. Active Projects

### 3.1 RSprint — Passage Anchoring, Reader Continuity, and Inline Commentary

**Status:** 🟡 In progress — RS-A through RS-E complete (see `HISTORY.md`), RS-F next

- [ ] **RS-F — Original Context**
  - [ ] Phase F1 — Retrieve original version content
  - [ ] Phase F2 — Navigate to original anchor
  - [ ] Phase F3 — UI integration ("View original context")

- [REMOVED] **RS-G — AI-Assisted Relocation** — removed 2026-08-17.
  When anchor-text matching fails after a sync, the comment is demoted to scene level
  automatically. The comment is not lost; it remains visible in the scene without a
  highlighted passage. This is the confirmed fallback behaviour — no AI required.

- [ ] **RS-H — Reader Insight**
  - [ ] Phase H1 — Progress tracking (anchor-based)
  - [ ] Phase H2 — Author insight (reader activity)
  - [ ] Phase H3 — UI (drill-down and indicators)

---

### 3.2 Go-Live Prerequisites

- [x] Invitation acceptance flow does not expose stored email (verified 2026-08-17)
- [x] Forgot-password flow works end-to-end in production (verified 2026-08-17)
- [x] Production smoke check: no `localhost` links, no plaintext email leakage (verified 2026-08-17)
- [x] Data handling aligns with UK GDPR and Data Protection Act 2018 (reviewed 2026-08-17)
- [x] Copy production `EmailProtection:EncryptionKey` and `EmailProtection:LookupHmacKey` into secure password manager (done 2026-08-17)
- [x] Apply pending EF migrations to production DB — all 27 migrations applied and in sync (verified 2026-08-17)
- [ ] UAT: complete scenarios C–K (A, B partial, H passed — see `DraftView-UAT-Plan.md`)
- [ ] Go-Live Day: send password reset emails to Becca (becca@the-dunlops.co.uk) and Hilary (hilaryrrb@gmail.com)

---

### 3.3 Platform Hardening

- [x] Fail2ban setup on production VM (verified active 2026-08-17 � sshd jail, 44 IPs banned since launch)
- [ ] Report Fault modal (HomeController POST + `_Layout.cshtml` modal + CSS)
- [ ] SystemStateMessage expiry (`ExpiresAt` nullable DateTime, `GetActiveAsync` filters expired)
- [ ] Logging: failed authorization attempts
- [x] CHANGE-010 — Impersonation — read-only reader view (implemented on change/CHANGE-010-reader-impersonation)

---

### 3.4 Multi-Tenancy Sprint Series

**🔴 HIGH PRIORITY** — A second author has expressed interest in the platform. Readers may read
books from multiple authors. Authors may also be beta readers for other authors. Multi-tenancy
is now a near-term requirement, not a post-revenue concern.

**Key cross-cutting implications:**
- `ReaderAccess` is currently scoped per-project. Reader Dashboard and future features need
  cross-project queries (all books for a reader, last read across all projects).
- Authors who are also beta readers need role-switching or a dual-role model.
- The Reader Dashboard (RD-Sprint, see section 3.8) depends on multi-tenancy for its
  "Books available" and "Discover authors" sections.
- `IProjectRepository` needs a `GetAllForReaderAsync(Guid userId)` method.

See `MultiTenancy.md` for full design, migration strategy, and sprint plan.

| Sprint | Deliverable |
|--------|-------------|
| MT-Sprint-1 | Account / Tenancy / TenancyMembership entity split |
| MT-Sprint-2 | Subscription enforcement, `IBillingProvider`, Creem integration |
| MT-Sprint-3 | Author self-serve registration, Dropbox connect per Tenancy |
| MT-Sprint-4 | Reader cross-tenancy identity |
| MT-Sprint-5 | Reader Marketplace (post-revenue) |

**Prerequisite:** Billing abstraction in place and production stable before MT-Sprint-1.

---

### 3.5 Dropbox Webhook Sync Sprint Series
See `DropBox Synchronisation Using WebHooks.md` for full architecture, control model, and sprint plan.
S-Sprint-1 complete — see `HISTORY.md`.

- [ ] **S-Sprint-2 — Webhook receipt and durable request recording**
  - [ ] Phase 1: Webhook endpoint surface
  - [ ] Phase 2: Signature validation and request parsing
  - [ ] Phase 3: Request recording service
  - [ ] Phase 4: Web endpoint tests
- [ ] **S-Sprint-3 — Immediate orchestration path**
  - [ ] Phase 1: Sync lease service
  - [ ] Phase 2: Cooldown hold evaluation
  - [ ] Phase 3: Background sync orchestration service shell
  - [ ] Phase 4: Orchestration tests
- [ ] **S-Sprint-4 — Dropbox delta interrogation and incremental download**
  - [ ] Phase 1: Cursor integration
  - [ ] Phase 2: Relevant-path filtering
  - [ ] Phase 3: Incremental download integration
  - [ ] Phase 4: Dropbox delta tests
- [ ] **S-Sprint-5 — Reuse existing sync pipeline end to end**
  - [ ] Phase 1: Existing pipeline integration seam
  - [ ] Phase 2: End-to-end background sync execution
  - [ ] Phase 3: Failure and recovery handling
  - [ ] Phase 4: Integration tests
- [ ] **S-Sprint-6 — Periodic worker and held request recovery**
  - [ ] Phase 1: Worker host and scheduling
  - [ ] Phase 2: Batch selection and bounded processing
  - [ ] Phase 3: Held request recovery
  - [ ] Phase 4: Worker tests
- [ ] **S-Sprint-7 — Stale reconciliation and operational hardening**
  - [ ] Phase 1: Daily stale reconciliation
  - [ ] Phase 2: Diagnostics and audit logging
  - [ ] Phase 3: Manual operational controls
  - [ ] Phase 4: Browser and operational verification
- [ ] **S-Sprint-8 — Daily health check and reconciliation app**
  - [ ] Phase 1: Separate console app scaffolding
  - [ ] Phase 2: Stale project reconciliation with lease-based protection
  - [ ] Phase 3: Cursor health and abandoned lease cleanup
  - [ ] Phase 4: Full rescan orchestration and operational verification

---

### 3.6 Incremental Refactor Roadmap
See `REFACTORING.md` for full detail. Phase 1 complete — see `HISTORY.md`.

**Status:** 🔴 HIGH PRIORITY — Web layer review (2026-08-17) identified ~530 lines of business logic violations. See `Web-Layer-Business-Logic-Review.md` and GitHub issue for full detail.

**Current Focus:** Web Layer Business Logic Extraction (replaces original Phase 2)

- [ ] **Phase 2a — Extract Critical Controller Orchestration** (🔴 BLOCKING)
  - [ ] Create `ISectionManagementService.GetSectionsSummaryAsync(projectId)` — extract `AuthorController.Sections` (lines 289-356)
  - [ ] Create `IProjectManagementService.AddDiscoveredProjectsAsync(selectedUuids, authorId)` — extract `AuthorController.AddProjects` (lines 985-1077)
  - [ ] Create `ICommentDisplayService.GetCommentDisplayDataAsync(...)` — extract `BaseReaderController.BuildCommentDisplayModelsAsync` (lines 263-355)
  - [ ] Full TDD: failing tests → implementation → zero regressions
- [ ] **Phase 2b — Standardize Background Sync**
  - [ ] Create `ISyncOrchestrationService.StartSyncAsync(projectId, authorId)`
  - [ ] Remove inline `Task.Run` blocks from `AuthorController.Sync` and `AddProjects`
- [ ] **Phase 2c — Extract Domain Mutations**
  - [ ] Move project activation/deactivation to `IProjectManagementService`
  - [ ] Move version deletion rules to `IVersioningService`
  - [ ] Remove all direct entity mutations from controllers
- [ ] **Phase 2d — Extract Helper Orchestration**
  - [ ] Create `IContentNavigationService` for tree-building helpers
  - [ ] Extract reader management summary logic to `IReaderManagementService`

**Original roadmap (deferred pending Web layer cleanup):**
- [ ] Phase 3 — Decompose startup/seeding
- [ ] Phase 4 — Standardise inheritance and shared utilities
- [ ] Phase 5 — Extract remaining procedural workflows

---

### 3.7 RD-Sprint — Reader Dashboard

**Status:** 🔵 Pre-planning

**Goal:** Replace the current reader entry point (`MobileChapters` for a single project) with a
proper Reader Dashboard that works across multiple authors' books and gives readers a clear
re-engagement surface.

**Routing (role-based home):**
- `Role.Author` → `/Author/Dashboard` (existing)
- `Role.BetaReader` → `/Reader/Dashboard` (new cold landing)
- Authors who are also beta readers need a role-switcher or a secondary dashboard link

**Dashboard URL:** `draftview.co.uk/Reader/Dashboard`

**Page sections (in order):**

1. **Comment Replies** *(conditional hero — only shown when unread replies exist)*
   - Shows all comments where `ParentComment.AuthorId == currentUserId`, ordered by most
     recent reply first, capped at ~10
   - Each card: who replied, which book + chapter/scene, snippet of original comment +
     snippet of reply, timestamp, "View in context" link
   - When empty: section is hidden entirely — book list takes the full page

2. **Continue Reading** *(single CTA)*
   - Most recent read position across all books, surfaced as a prominent button
   - Needs cross-project `IReadingProgressService.GetLastReadEventAcrossProjectsAsync(userId)`

3. **My Books** *(list)*
   - Every project the reader has `ReaderAccess` to, ordered by most recently read
   - Each card: book title, author name, unread chapter count ("2 new chapters" / "up to date"),
     reply count badge if any replies exist for that book
   - Links into `MobileChapters` filtered to that project

4. **Discover Authors** *(link → `/Reader/Discovery`)*
   - Discovery page shows authors who have opted in (`IsOpenToReaders` flag on project/tenancy)
   - Each listing: author name, book title, brief synopsis, "Request access" button
   - `IsOpenToReaders` is a small addition to the Project (or future Tenancy) entity

**Infrastructure needed (new — not yet built):**
- `ICommentRepository.GetRepliesToUserCommentsAsync(Guid userId)` — joins reply → parent,
  filters `parent.AuthorId == userId`, includes section + project context
- `IReadingProgressService.GetLastReadEventAcrossProjectsAsync(Guid userId)`
- `IProjectRepository.GetAllForReaderAsync(Guid userId)` — cross-project reader query
- `Project.IsOpenToReaders` flag (Domain entity + EF migration)
- `ReaderController.Dashboard()` action + `ReaderDashboardViewModel`
- `ReaderController.Discovery()` action + `ReaderDiscoveryViewModel`
- `DraftView.Web/Views/Reader/ReaderDashboard.cshtml`
- `DraftView.Web/Views/Reader/ReaderDiscovery.cshtml`
- Home controller role-based redirect: `/` → author or reader dashboard by role

**Assets:**
- `DraftViewReaderDash.Hero.png` — hero image for the dashboard cold landing. Added to main 2026-08-16.

**Dependency:** Shares infrastructure with MT-Sprint (cross-project queries, tenancy model).
Plan RD-Sprint-1 after MT-Sprint-1 lands.

| Sprint | Deliverable |
|--------|-------------|
| RD-Sprint-1 | Dashboard shell + Comment replies section (single-author, single-project) |
| RD-Sprint-2 | Continue reading + My Books (cross-project, requires MT-Sprint-1) |
| RD-Sprint-3 | ~~Discover Authors page~~ — superseded by DR-Sprint (complete 2026-08-17) |

---

### 3.8 DR-Sprint — Open Book Discovery & Access Requests

**Status:** ✅ Complete — all 5 phases merged to main 2026-08-17. See `HISTORY.md` for full detail.

**Note:** RD-Sprint-3 ("Discover Authors") is superseded by DR-Sprint.

---

### 3.9 Post Go-Live Backlog

- Ubuntu OS upgrade: 20.04 ? 22.04 ? 24.04 via `do-release-upgrade` from Oracle Cloud console (pg_dump first; keep current config files when prompted; verify all services after each hop)
- Reader notification emails (new chapter published)
- Dropbox OAuth2 token refresh
- Dropbox webhook controller for push-based sync
- In-app Dropbox re-auth page
- Author/Comments view (mobile)
- Author Chapter Page (`Author/Chapter/{id}`)
- Publishing cascades (part-level, book-level)
