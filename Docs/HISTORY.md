# DraftView — Completed Work History
Last updated: 2026-08-31

---

## Completed Sprints

| Sprint | Deliverable | Date |
|--------|-------------|------|
| DR-Sprint | Open Book Discovery & Access Requests — 5 phases, `AccessRequest` entity, discovery page, author review UI, reader dashboard requests section, CSS. See DR-Sprint section below. | 2026-08-17 |
| V-Sprints 1–10 | Publishing and Versioning Series (636 tests). See `Publishing And Versioning Architecture.md` | pre-2026-04 |
| Sprint 4 | Email Privacy and Controlled Access. See `Sprint4-EmailPrivacy.md` | pre-2026-04 |
| Sprint 3 | Reader Font Preferences | pre-2026-04 |
| Sprint 2 | Reader Experience | pre-2026-04 |
| Sprint 1 | Pre-Beta Push | pre-2026-04 |
| Email Sprint | Oracle Email Delivery, MailKit, DKIM, SPF | pre-2026-04 |
| Role Migration | Identity roles, SystemSupport, SystemStateMessage, mobile reader flow | pre-2026-04 |
| ScrivenerProject → Project rename | — | pre-2026-04 |
| UserNotificationPreferences → UserPreferences rename | — | pre-2026-04 |
| Incremental Refactor Phase 1 | Centralise controller user/role resolution | pre-2026-04 |

---

## DR-Sprint — Open Book Discovery & Access Requests (2026-08-17)

All 5 phases complete and merged to main.

- **Phase 1 — Domain & Infrastructure:** `IsOpen`/`Brief`/`OpenedAt` on `ScrivenerProject`; `ReaderBio`/`ReaderGenreInterests`/`ReaderPace` on `UserPreferences`; new `AccessRequest` entity + `AccessRequestStatus` enum; `NotificationEventType.AccessRequest`; `IAccessRequestRepository`; EF migration `AddOpenBookDiscovery`.
- **Phase 2 — Application Layer:** `IAccessRequestService`/`AccessRequestService`; open/close toggles with bulk-decline and `OpenedAt`; acceptance email template.
- **Phase 3 — Author UI:** Publishing page open/close toggle + brief textarea; book list pending badge; `Author/BookRequests` page with Accept/Decline actions.
- **Phase 4 — Reader/Discovery UI:** `Discovery/Index` public page; inline request form; reader dashboard "Your requests" section with `MarkDeclinedAsSeenAsync`; reader profile card in Account Settings; safe landing link; `/discover` nav link.
- **Phase 5 — CSS:** Discovery cards, request list, reader profile, dashboard requests sections; CSS version bump to `v2026-08-16-5`.

---

## Completed RSprint Phases

- **RS-A — Anchor Foundation**
  - Phase A1 — Model discovery (Copilot-led inspection and proposal)
  - Phase A2 — Domain definition (TDD)
  - Phase A3 — Persistence (migration, additive only)
  - Phase A4 — Application surface (creation/retrieval)

- **RS-B — Anchored Resume**
  - Phase B1 — Capture anchor from reading position
  - Phase B2 — Restore using matching pipeline
  - Phase B3 — Integration with ReadEvent
  - Phase B4 — Tests (cross-version resume)

- **RS-C — Inline Comments**
  - Phase C1 — Selection capture
  - Phase C2 — Comment creation with anchor
  - Phase C3 — Rendering (inline indicators)
  - Phase C4 — Tests

- **RS-D — Deterministic Relocation**
  - Phase D1 — Exact matching
  - Phase D2 — Context matching
  - Phase D3 — Fuzzy matching
  - Phase D4 — Confidence scoring
  - Phase D5 — Integration and tests

- **RS-E — Human Override**
  - Phase E1 — Permission enforcement (reader + author only)
  - Phase E2 — Reject match ("wrong place")
  - Phase E3 — Relink to new passage
  - Phase E4 — Status tracking (actor + timestamp)

---

## Completed S-Sprint Phases

- **S-Sprint-1 — Foundation for background Dropbox sync**
  - Phase 1: Architecture and task alignment
  - Phase 2: Domain model for sync control
  - Phase 3: Domain tests for control rules
  - Phase 4: Infrastructure mapping and migration

---

## Bugs Fixed

- Issue #96 — `GetCurrentUserAsync` used `User.Identity.Name` (the Identity `UserName`) as the email address for HMAC domain-user lookup. Readers who chose a custom username at invitation acceptance (e.g. `JennyMoss`) had `UserName ≠ Email`, so the lookup returned null and every controller action returned `Forbid()` → AccessDenied. Fixed by reading `ClaimTypes.Email` first (already present in the sign-in cookie via the default Identity `UserClaimsPrincipalFactory`), falling back to `User.Identity.Name` for sessions where `UserName == Email`. Confirmed on production: Jenny was unblocked immediately after deploy without requiring a role or data change. Fixed in PR #97, `BaseController.GetCurrentUserAsync()` (2026-08-30)
- Issue #94 — `IsReaderActive` flag incorrectly gated readers with explicit `ReaderAccess` grants in `ReaderController`. `DesktopDashboard`, `MobileDashboard`, and `Chapters` all checked `!project.IsReaderActive` before serving content to readers, but `IsReaderActive` controls only the public request-access flow — it must not affect readers already granted explicit access. `IsSoftDeleted` is the correct gate. Removed `!project.IsReaderActive` from all three guards. Fixed in PR #98 (2026-08-30)
- Issue #84 — Sync service aborted on blank Scrivener section titles (`InvariantViolationException` → `Error` status with no visible cause in UI). Fixed in PR #85: title sanitized to `"Untitled"` in `ScrivenerSyncService.ReconcileNodeAsync` before reaching domain factories; sync now completes as `Healthy` with a warning log. `SyncErrorMessage` is also surfaced as inline text beneath the Error badge on Author Dashboard so authors can diagnose failures without leaving the page. `PostgreSQL.md` updated with diagnostic query and common error message table (2026-08-29)
- BUG-025 — `InvitationRepository.GetByUserIdAsync` had no ORDER BY; with multiple invitations per user (cancelled + pending), it could return the cancelled one, making an invited reader appear as Inactive rather than Invited in the Readers list. Fixed by switching the Readers action to `GetPendingByUserIdAsync` (2026-08-15)
- BUG-024 — Reader was not assigned the `BetaReader` Identity role on invitation acceptance; `AcceptInvitation` POST created the Identity user but never called `AddToRoleAsync`, so the session cookie had no role claim and all reader pages returned 403 Access Denied. Fixed by calling `AddToRoleAsync` immediately after `CreateAsync` (2026-08-15)
- BUG-023 — ASP.NET Core DataProtection used an ephemeral in-memory key ring that regenerated on every service restart, silently invalidating antiforgery tokens from pre-restart sessions and causing unhandled 500 errors on all form POSTs around a restart. Fixed by persisting keys to `/var/www/draftview-keys`; path is configurable via `DataProtection:KeysPath` (falls back to ephemeral when unset, e.g. in development) (2026-08-15)
- BUG-022 — Inviting a reader always crashed with the controlled error page; `App:BaseUrl` was absent from production config, causing `GetConfiguredAppBaseUrl()` to throw on every invitation attempt. Added `App:BaseUrl` to `appsettings.Production.json` and updated the publish script to deploy it alongside the app (2026-08-15)
- BUG-021 — Add Projects page stalled on foreground Dropbox vault listing; GET now returns page shell immediately, vault list fetched via AJAX from new `DiscoverProjects` endpoint (2026-08-14)
- BUG-020 — BetaBooksImporter broken by email encryption-at-rest migration; fixed email lookup to use `IUserRepository.GetByEmailAsync` (HMAC-based), and key loading to use configured `EmailProtection__EncryptionKey`/`EmailProtection__LookupHmacKey` env vars instead of random parameterless-ctor keys (2026-08-14)
- BUG-019 — Add Project timed out (Cloudflare 524) on large Scrivener projects; `ParseProjectAsync` was awaited on the HTTP request thread in `AddProjects`; fixed by calling `MarkSyncing()`, saving, then firing `Task.Run` with `IServiceScopeFactory` scope (matching the existing `Sync` action pattern); Dashboard progress bar now activates automatically on redirect (2026-08-14)
- BUG-018 — Reader view did not display scene version number; DesktopRead and MobileRead now render a persistent scene version label from existing `CurrentVersionNumber` (`vN`) independent of update-banner state (2026-04-21)
- BUG-017 — Sections view did not clearly surface pending synced scene changes; added explicit chapter-level "Pending changes" indication for published chapters with changed child scenes (2026-04-21)
- BUG-016 — Publishing page leaked raw Razor token for version label; scene version hint now renders explicitly as text (e.g. `v3`) instead of showing `v@doc.CurrentVersionNumber` (2026-04-21)
- BUG-015 — Reader showed unpublished content and inconsistent banner version; now pinned to latest `SectionVersion` with stable versioned banner rendering (2026-04-21)
- BUG-014 — Republishing a chapter created new versions for all scenes unconditionally; fixed to only create versions for scenes with `ContentChangedSincePublish = true` or no existing version (2026-04-20)
- BUG-013 — Reader Account Settings missing font/size preferences; `AccountController.Settings` now uses `BaseController` role helpers to correctly identify BetaReader users (2026-04-20)
- BUG-012 — New scene added in Scrivener did not trigger republish prompt; reconciliation now marks published parent chapter changed on new child scene creation (2026-04-20)
- BUG-010 — Publishing page has no navigation link from Sections view or Dashboard (2026-04-20)
- BUG-009 — New scene added in Scrivener did not appear after incremental sync; fixed by running `ReconcileProjectFromScrivxAsync` in the incremental path so new binder UUIDs are created from the cached local `.scrivx` without additional Dropbox API round-trips (2026-04-20)
- BUG-008 — Author/Section view had unreadable light-on-light prose and inconsistent visual design; removed inline styling, applied dark-theme token-based styling, and aligned breadcrumb/metadata/comments with author UI patterns (2026-04-20)
- BUG-007 — Activating a project now atomically deactivates the current active project (2026-04-20)
- BUG-006 — Unable to sync projects — seeder author lookup now Identity-ID-first; invalid ciphertext repaired on startup; duplicate author row repair added (2026-04-20)
- BUG-005 — Password reset link immediately expired — reset flow now resolves Identity user by email fallback (2026-04-19)
- BUG-004 — ForgotPassword returns HTTP 405 in production — two missing migrations applied; status code routing fixed (2026-04-19)
- BUG-003 — Settings surfaced ciphertext errors; now logs and redirects to error page instead of exposing exceptions (2026-04-21)
- BUG-002 — System Support had no readers page; added `GET /Support/Readers` listing readers by display name and status only (2026-04-20)
- BUG-001 — Reader removal not reflected in UI; repository now filters `!IsSoftDeleted` (2026-04-20)
- Production database migration drift — reader page failed because PassageAnchor rejection audit columns were missing; resolved with corrective migration `20260427123533_ApplyMissingPassageAnchorRejectionAudit` (2026-04-27)
- Empty EF migration guard — added Roslyn-based infrastructure test for empty `Up(MigrationBuilder)` methods; allows only legacy exception `20260427121437_AddPassageAnchorFields.cs` (2026-04-27)
- Cross-platform local cache path resolution — `IPlatformPathService`, platform-aware fallback (BugFix-Mac, 2026-04-19)
- `/Author/InviteReader` submit production crash — operational failures route to `Home/Error` (BugFix-Mac, 2026-04-19)
- MailKit NU1902 vulnerability — upgraded to 4.16.0 (BugFix-PC, 2026-04-19)
- Reader view does not apply saved Reading Preferences (2026-04-17)
- CS9107 in `AccountController` primary constructor (2026-04-17)
- Reader/Read mobile view 404
- Reader/Read comment box overflows page boundary on RHS
- AddComment POST redirects to top of page — fixed with `#scene-{id}` anchors
- Author/Dashboard Recent Activity truncation — replaced with persisted `AuthorNotification`
- Login always redirected to Reader/Dashboard — fixed role-based redirect
- Reader diff UX for removed paragraphs — thin markers instead of strikethrough

---

## AI Feature Removals (2026-08-17)

- **AI summaries removed** — `AiSummaryService`, `IAiSummaryService`, `AiSummary` property on `SectionVersion`, EF column, DI registration, viewmodel properties, controller tuple member, and all Razor render blocks stripped. EF migration `RemoveAiSummaryFromSectionVersion` drops the column. Avoids AI dependency in publishing workflow.
- **RS-G (AI-Assisted Relocation) removed** — When anchor-text matching fails after a sync, the comment is demoted to scene level automatically. The comment is not lost; it remains visible in the scene without a highlighted passage. This is the confirmed fallback behaviour — no AI required.

---

## Completed Changes

- CHANGE-022 — Section read-count tooltip shows reader names — merged 2026-08-30 (PR #91). `SectionReadCountDto` carries a `ReaderNames` list; `GetSectionReadCountsAsync` populates it via a join to `AppUsers`; Sections view renders the names as a `title` attribute tooltip on the read-count badge.
- CHANGE-021 — Activity filter groups fix — merged 2026-08-29 (PR #90). Replaced single-event-type filter with `NotificationFilterGroup` enum. 'Readers' now covers ReaderJoined, ReaderReadNewScene, ReaderReturned, ReaderFinishedManuscript, AccessRequest; 'Sync' covers SyncCompleted and ChapterUploaded. `GetByAuthorIdAndTypesAsync` added to repository (TDD, 2 tests). Service resolves group → type list via static dictionary.
- CHANGE-020 — Settings Activity Log corrected — merged 2026-08-29 (PR #88). Removed full notification list from Settings; replaced with checkbox rows (one per type) each with a scoped Clear button and a confirm-guarded Clear All. Activity log remains on Dashboard only.
- CHANGE-019 — Recent Activity: filter chips on Dashboard + Activity Log with scoped clear on Settings — merged 2026-08-29. Dashboard gains server-side filter chips (All/Comments/Replies/Readers/Sync) via `?type=X`; "Clear All" removed from Dashboard. Account/Settings gets Activity Log card with same filter pills; "Clear [Type]" (no confirm) when filtered; "Clear All" guarded by `confirm()`. `_NotificationItem.cshtml` partial extracted. `IAuthorNotificationRepository` + `IDashboardService` extended with type-filter methods (TDD, 8 new tests). CSS version `v2026-08-29-1`.
- CHANGE-009 — Mobile reader: read-first scene comments — merged 2026-08-16. `MobileRead` becomes prose + bottom nav only; `.mobile-comments` section removed; "Scene Comments (N) ›" link navigates to new `GET /Reader/SceneComments/{sceneId}` page; `MobileReadViewModel` carries only `SceneCommentCount` int.
- CHANGE-008 — Mobile reader: chapter comments page + disable passage-anchor capture on touch — merged 2026-08-16. Passage-anchor creation disabled on mobile; chapter-level comments moved to dedicated `GET /Reader/ChapterComments/{chapterId}` page.
- CHANGE-006 + CHANGE-007 — Collapsible reader nav and comments toggle — merged 2026-08-15. Collapsible act tree with `›` chevron toggles, panel pin/unpin, `localStorage` state persistence.
- CHANGE-005 — Username login: `AcceptInvitation` form now includes a "Choose a username" field (`autocomplete="username"`, stored as `IdentityUser.UserName`). Login accepts username or email — if the input contains `@` and the initial attempt fails, Identity looks up the user by email and retries with their `UserName`. Display name is not a valid login identifier (2026-08-15)
- CHANGE-004 — Readers list: Resend Invitation button (paper-plane icon) for readers in Invited state, and for Active readers who still have a pending invitation (manually activated before completing setup). `ResendInvitation` action deactivates the reader first if needed, then re-issues the invitation preserving the original expiry policy (2026-08-15)
- CHANGE-002 — `Views/Author/Publishing.cshtml`: align scene version labels beside scene titles using CSS Grid layout (2026-04-21)
- CHANGE-001 — `Views/Reader/DesktopRead.cshtml` & `MobileRead.cshtml`: moved scene version labels from main title area to left-hand navigation (desktop) and top nav metadata (mobile) for reduced reading noise (2026-04-21)
