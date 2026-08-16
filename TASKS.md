# DraftView — Task List
Last updated: 2026-08-16 (late evening)

---

## 0. Summary

**Live at:** https://draftview.co.uk
**Production:** Oracle Cloud VM `141.147.71.62`, .NET 10, PostgreSQL, Nginx, Cloudflare SSL
**Repository:** https://github.com/aclarke6/DraftView

### Current Test State
- 883 total, 882 passed, 1 skipped, 0 failed
- 1 skipped — `SmtpEmailSenderIntegrationTests` (sends real email, manual only)

### Active Work
| Track | Status |
|-------|--------|
| RSprint Series | 🟡 In progress — RS-A to RS-E complete, RS-F next |
| S-Sprint Series | 🟡 In progress — S-Sprint-1 complete, S-Sprint-2 next |
| MT-Sprint Series | 🔴 HIGH PRIORITY — second author interest accelerates timeline; see `MultiTenancy.md` |
| RD-Sprint Series | 🔵 Pre-planning — Reader Dashboard; see section 3.7 |
| DR-Sprint Series | 🔵 Design complete — Open Book Discovery & Access Requests; see section 3.8 |
| Go-Live Prerequisites | 🔴 Blocking — items below must complete before launch |
| UAT | 🟡 In progress |

---

## 1. Reference Documents

| Document | Purpose |
|----------|---------|
| `AGENTS.md` | Authoritative execution rules for all coding agents — defines constraints, architecture boundaries, TDD requirements, and hard-gated response behaviour across all tools |
| `Passage Anchoring, Reader Continuity, and Inline Commentary.md` | Authoritative design for passage anchoring, relocation, reader continuity, and inline commentary (RSprint series) |
| `AIScoringService.md` | AI change scoring service — provider abstraction, tier model, and usage for relocation confidence (RS-G) |
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

- [x] CHANGE-008 — Mobile reader: chapter comments page + disable passage-anchor capture on touch

  **Implemented:** branch `change/CHANGE-008-mobile-chapter-comments`

  **Scope — mobile only (desktop unchanged):**
  - Passage-anchor *creation* (selection capture) disabled on mobile — unreliable on touch. Existing
    anchored comments still display with highlight + modal. Two IIFEs removed from `MobileRead.cshtml`:
    `CapturePassageAnchorSelection` and `applyPendingAnchor`.
  - Scene-level comments stay inline in `MobileRead`. Add-comment form moved to **top** of comments
    section so it appears before the list.
  - Chapter-level comments get a dedicated page:
    `GET /Reader/ChapterComments/{chapterId}` → `MobileChapterComments.cshtml`
  - "Chapter Comments →" link added at the bottom of `MobileRead` and `MobileScenes` views.

  **Files modified:**
  - MODIFY: `DraftView.Web/Views/Reader/MobileRead.cshtml` — remove two selection-capture IIFEs; move
    add-comment form to top of comments section; add chapter-comments link
  - MODIFY: `DraftView.Web/Views/Reader/MobileScenes.cshtml` — add chapter-comments link
  - MODIFY: `DraftView.Web/Models/MobileReaderViewModels.cs` — add `MobileChapterCommentsViewModel`
  - MODIFY: `DraftView.Web/Controllers/ReaderController.cs` — add `ChapterComments(Guid chapterId)` action
  - NEW: `DraftView.Web/Views/Reader/MobileChapterComments.cshtml` — chapter comments page view
  - MODIFY: `DraftView.Web/wwwroot/css/DraftView.MobileReader.css` — chapter-comments link + page styles

- [x] CHANGE-009 — Mobile reader: read-first scene comments — merged to main 2026-08-16. See `HISTORY.md`.

  **Goal:** Remove inline scene comments from `MobileRead` entirely. The reading surface should be
  prose only. Comment activity is accessed deliberately via a count link after reading.

  **Design:**
  - `MobileRead` becomes prose + bottom nav only. The `.mobile-comments` section is removed.
  - A "Scene Comments (N) ›" link sits at the bottom of the prose (below the bottom nav).
    If there are no comments, shows "Scene Comments ›" with no count.
  - Tapping the link navigates to a new `GET /Reader/SceneComments/{sceneId}` page.
  - `MobileSceneComments.cshtml` mirrors `MobileChapterComments.cshtml` — full add/edit/delete/reply,
    back link returns to `Read` (the prose page).
  - `MobileReadViewModel` no longer carries the full comment list — only a `SceneCommentCount` int,
    avoiding the expensive `BuildCommentDisplayModelsAsync` call on every scene page load.
  - Post-comment redirects updated: `AddComment` / `DeleteComment` / `EditComment` from the scene
    comments page redirect back to `SceneComments`, not `Read`.

  **Files affected:**
  - MODIFY: `DraftView.Web/Models/MobileReaderViewModels.cs` — replace Comments list with
    `SceneCommentCount`; add `MobileSceneCommentsViewModel`
  - MODIFY: `DraftView.Web/Controllers/ReaderController.cs` — update `MobileRead` private method;
    add `SceneComments(Guid sceneId)` action
  - MODIFY: `DraftView.Web/Controllers/BaseReaderController.cs` — extend `RedirectToReaderAsync`
    to route Document IDs to `SceneComments` on mobile
  - MODIFY: `DraftView.Web/Views/Reader/MobileRead.cshtml` — remove comments section; add count link
  - NEW: `DraftView.Web/Views/Reader/MobileSceneComments.cshtml`
  - MODIFY: `DraftView.Web/wwwroot/css/DraftView.MobileReader.css` — count link style; rename
    generic `.mobile-comments-link` to replace per-type classes
  - MODIFY: `DraftView.Web/wwwroot/css/DraftView.Core.css` — CSS version bump

---

## 3. Active Projects

### 3.1 RSprint — Passage Anchoring, Reader Continuity, and Inline Commentary

**Status:** 🟡 In progress — RS-A through RS-E complete (see `HISTORY.md`), RS-F next

- [ ] **RS-F — Original Context**
  - [ ] Phase F1 — Retrieve original version content
  - [ ] Phase F2 — Navigate to original anchor
  - [ ] Phase F3 — UI integration ("View original context")

- [ ] **RS-G — AI-Assisted Relocation**
  - [ ] Phase G1 — Integration via AIScoringService
  - [ ] Phase G2 — Prompt design and candidate matching
  - [ ] Phase G3 — Confidence thresholds and activation

- [ ] **RS-H — Reader Insight**
  - [ ] Phase H1 — Progress tracking (anchor-based)
  - [ ] Phase H2 — Author insight (reader activity)
  - [ ] Phase H3 — UI (drill-down and indicators)

---

### 3.2 Go-Live Prerequisites

- [ ] Add `Anthropic:ApiKey` to `appsettings.Production.json` (enables AI summaries)
- [ ] Invitation acceptance flow does not expose stored email
- [ ] Forgot-password flow works end-to-end in production
- [ ] Production smoke check: no `localhost` links, no plaintext email leakage
- [ ] Data handling aligns with UK GDPR and Data Protection Act 2018
- [ ] Copy production `EmailProtection:EncryptionKey` and `EmailProtection:LookupHmacKey` into secure password manager
- [ ] Go-Live Day: send password reset emails to Becca (becca@the-dunlops.co.uk) and Hilary (hilaryrrb@gmail.com)

---

### 3.3 Platform Hardening

- [ ] Fail2ban setup on production VM
- [ ] Report Fault modal (HomeController POST + `_Layout.cshtml` modal + CSS)
- [ ] SystemStateMessage expiry (`ExpiresAt` nullable DateTime, `GetActiveAsync` filters expired)
- [ ] Logging: failed authorization attempts
- [ ] Impersonation — read-only, explicit enter/exit mode (design agreed, not built)

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

- [ ] Phase 2 — Extract procedural controller workflows
- [ ] Phase 3 — Decompose startup/seeding
- [ ] Phase 4 — Standardise inheritance and shared utilities
- [ ] Phase 5 — Extract remaining procedural workflows
- [ ] Phase 6 — Standardise sync kickoff (remove inline `Task.Run`)

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
| RD-Sprint-3 | Discover Authors page + `IsOpenToReaders` opt-in |

---

### 3.8 DR-Sprint — Open Book Discovery & Access Requests

**Status:** 🔵 Design complete — ready to implement

**Goal:** Allow authors to open a project for discovery by readers. Readers browse open books,
submit access requests with an optional cover note and contact email. Authors review requests and
accept or decline. Accepted readers are added to the book directly; an email confirms acceptance.
The feature integrates with the existing AuthorNotification system and reader email pipeline.

---

#### Model summary

**`ScrivenerProject` extensions**
- `IsOpen: bool` — author toggles on Publishing page
- `Brief: string?` — pitch visible to readers (genre, word count, feedback wanted, content notes)
- `OpenedAt: DateTime?` — updated every time `IsOpen` is set to `true`; acts as the "clean slate" stamp

**`AccessRequest` (new entity)**
- `Id: Guid`
- `ReaderId: Guid` → User
- `ProjectId: Guid` → ScrivenerProject
- `CoverNote: string?` — optional, max 500 chars
- `ContactEmail: string?` — optional off-platform contact email
- `Status: Pending | Approved | Declined`
- `RequestedAt: DateTime`
- `RespondedAt: DateTime?`
- `SeenByReaderAt: DateTime?` — set when reader views a declined request on their dashboard

**`UserPreferences` extensions** (all nullable, all optional)
- `ReaderBio: string?`
- `ReaderGenreInterests: string?`
- `ReaderPace: enum? (Slow | Steady | Fast)`

**`NotificationEventType`** — add `AccessRequest`

---

#### Business rules

**Requesting**
- Book must be `IsOpen = true`
- Reader may not have an existing `Pending` request for the same project
- Reader shown at submission: "You'll be notified by email if the author accepts."

**Discovery page filter (per reader)**
Show the "Request access" button if:
- No `Declined` request exists for (reader, book)
- OR most recent `Declined` has `RespondedAt ≤ project.OpenedAt` (book reinstated since decline → fresh state)

**Declined entry visibility on reader dashboard**
```
Show if: Status = Pending
      OR (Status = Declined AND SeenByReaderAt IS NULL)
      OR (Status = Declined AND SeenByReaderAt.Date >= UtcNow.Date)
```
On dashboard load, set `SeenByReaderAt = UtcNow` for any visible Declined entry where it is null.
The following calendar day the entry vanishes permanently from all reader-facing queries.

**Accepting**
- Adds reader to project via existing grant-access mechanism
- Sends approval email to reader
- Marks request `Approved`, sets `RespondedAt`
- Other pending requests for the same book remain open (author can accept multiple readers)

**Declining**
- Marks request `Declined`, sets `RespondedAt`
- No email sent; reader sees "Not accepted" on their dashboard for one day, then vanished

**Revoking (`IsOpen → false`)**
- All `Pending` requests bulk-declined (`RespondedAt = now`)
- `OpenedAt` is NOT updated on revoke
- Existing approved readers keep their access

**Reinstating (`IsOpen → true`)**
- `OpenedAt` updated to now
- All previously declined readers may re-request (their `RespondedAt < new OpenedAt`)
- This includes readers declined in the original run — it is a fully fresh state

---

#### Phase 1 — Domain & Infrastructure

- [ ] **Phase 1.1** — Add `IsOpen`, `Brief`, `OpenedAt` to `ScrivenerProject`
- [ ] **Phase 1.2** — Add optional `ReaderBio`, `ReaderGenreInterests`, `ReaderPace` to `UserPreferences`
- [ ] **Phase 1.3** — New `AccessRequest` entity + `AccessRequestStatus` enum
- [ ] **Phase 1.4** — Add `NotificationEventType.AccessRequest` to existing enum
- [ ] **Phase 1.5** — `IAccessRequestRepository` interface:
  - `GetByIdAsync(id)`
  - `GetPendingByProjectIdAsync(projectId)` — for author's requests page + count
  - `GetVisibleByReaderIdAsync(readerId, today)` — Pending + visible Declined (per dashboard rule)
  - `GetPendingCountByProjectIdAsync(projectId)` — for book list badge
  - `AddAsync(request)`
  - `SaveAsync(request)` — for status updates
  - `BulkDeclineByProjectAsync(projectId, respondedAt)` — on revoke
  - `MarkDeclinedAsSeenAsync(readerId, today)` — sets SeenByReaderAt for unseen declined entries
- [ ] **Phase 1.6** — `AccessRequestRepository` implementation (InMemory tests)
- [ ] **Phase 1.7** — `DraftViewDbContext`: add `DbSet<AccessRequest>`; update `ScrivenerProject` config; update `UserPreferences` config
- [ ] **Phase 1.8** — EF migration: `AddOpenBookDiscovery`
- [ ] **Phase 1.9** — Register repository in DI (`ServiceCollectionExtensions`)
- [ ] Tests: `AccessRequestTests.cs`, `AccessRequestRepositoryTests.cs`

#### Phase 2 — Application Layer

- [ ] **Phase 2.1** — `IAccessRequestService` / `AccessRequestService`:
  - `SubmitRequestAsync(readerId, projectId, coverNote?, contactEmail?)` — validates open + no dupe, creates request, fires `AuthorNotification`
  - `ApproveRequestAsync(requestId, authorId)` — validates ownership, grants access, marks Approved, sends email
  - `DeclineRequestAsync(requestId, authorId)` — validates ownership, marks Declined, no email
  - `BulkDeclineOnRevokeAsync(projectId)` — called when `IsOpen` → false
- [ ] **Phase 2.2** — Extend project update logic: when `IsOpen` toggled off → call `BulkDeclineOnRevokeAsync`; when toggled on → update `OpenedAt`
- [ ] **Phase 2.3** — Email template: "Your request to read [Book Title] has been accepted" (existing email pipeline)
- [ ] **Phase 2.4** — Register service in DI
- [ ] Tests: `AccessRequestServiceTests.cs`

#### Phase 3 — Author UI

- [ ] **Phase 3.1** — Publishing page (`Author/Publishing.cshtml`):
  - Toggle: "Open for beta readers"
  - Textarea: "Brief for readers" (required when opening; hidden when closed)
  - Save triggers open/close logic via updated controller action
- [ ] **Phase 3.2** — Book list (existing author pages): pending request count badge per open book, links to Requests page
- [ ] **Phase 3.3** — New page `Author/BookRequests.cshtml` (route: `/author/projects/{projectId}/requests`):
  - Lists all Pending requests: reader display name, bio snippet, pace, cover note, contact email, date
  - **Accept** and **Decline** buttons per row
  - Accepted/declined rows disappear from the active list
  - If book is no longer Open: banner noting all requests have been declined
- [ ] **Phase 3.4** — `AuthorController` actions: `BookRequests(projectId)`, `ApproveRequest(requestId, projectId)`, `DeclineRequest(requestId, projectId)`
- [ ] Tests: controller unit tests for all three actions

#### Phase 4 — Reader/Discovery UI

- [ ] **Phase 4.1** — New public page `Discovery/Index.cshtml` (route: `/discover`):
  - Anonymous: book cards with "Sign in to request access" CTA
  - Logged-in reader, no existing request: "Request access" button
  - Logged-in reader, pending request: "Requested — awaiting response" (no button)
  - Logged-in reader, visible declined: "Not accepted" (no button)
  - Logged-in reader, approved: "You have access" (no button)
  - No open books: warm holding message
- [ ] **Phase 4.2** — Request form (modal or inline):
  - Optional cover note (textarea, 500 char limit shown)
  - Optional contact email (pre-fills from account email, editable)
  - Informational note shown before submitting
- [ ] **Phase 4.3** — Reader dashboard (pending requests section):
  - "Your requests" list: Pending + visible Declined entries
  - Declined shows "Not accepted" label
  - `MarkDeclinedAsSeenAsync` called on load
- [ ] **Phase 4.4** — Reader profile card in Account Settings:
  - Bio (textarea)
  - Genre interests (text input)
  - Reading pace (dropdown: Slow / Steady / Fast)
  - All optional, saved to `UserPreferences`
- [ ] **Phase 4.5** — Safe landing: reader with no active books redirected to `/discover`
- [ ] **Phase 4.6** — "Browse" nav link updated to point to `/discover`
- [ ] **Phase 4.7** — `DiscoveryController` (or new actions on `HomeController`):
  - `Index()` — public; populates per-reader request state if authenticated
  - `SubmitRequest(projectId, coverNote?, contactEmail?)` — authenticated reader only
- [ ] Tests: discovery integration tests (anonymous access, authenticated request, duplicate guard)

#### Phase 5 — CSS & version bump

- [ ] Discovery cards: `.discovery-card`, `.discovery-card__brief`, `.discovery-card__cta`, `.discovery-card__status`
- [ ] Requests page: `.request-list`, `.request-list__item`, `.request-list__meta`, `.request-list__actions`
- [ ] Reader profile section: `.reader-profile-card`
- [ ] CSS version bump

---

**Note:** RD-Sprint-3 ("Discover Authors") is superseded by DR-Sprint. RD-Sprint-3 can be marked
complete once DR-Sprint ships.

---

### 3.9 Post Go-Live Backlog

- Reader notification emails (new chapter published)
- Dropbox OAuth2 token refresh
- Dropbox webhook controller for push-based sync
- In-app Dropbox re-auth page
- Author/Comments view (mobile)
- Author Chapter Page (`Author/Chapter/{id}`)
- Publishing cascades (part-level, book-level)
