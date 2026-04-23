# DraftView — Task List
Last updated: 2026-04-21

---

## 0. Summary

**Live at:** https://draftview.co.uk
**Production:** Oracle Cloud VM `193.123.182.208`, .NET 10, PostgreSQL, Nginx, Cloudflare SSL
**Repository:** https://github.com/aclarke6/DraftView

### Current Test State
- 770 total, 769 passed, 1 skipped, 0 failed (post BUG-018 work)
- 1 skipped — `SmtpEmailSenderIntegrationTests` (sends real email, manual only)

### Active Work
| Track | Status |
|-------|--------|
| V-Sprints 1–10 | ✅ All complete |
| RSprint-1 | 🔵 Planned — reader and author experience improvements |
| MT-Sprint Series | 🔵 Pre-planning — see `MultiTenancy.md` |
| S-Sprint Series | 🟡 In progress — S-Sprint-1 Phase 1 |
| BugFix-Mac | 🟢 Synced with main, awaiting next bug |
| BugFix-PC | 🟢 Merged to main |
| UAT | 🟡 In progress — 2026-04-20 |

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

---

## 2. Open Minor Work

### 2(a) Bugs


### 2(b) Changes

- [DONE] CHANGE-001 — `Views/Reader/DesktopRead.cshtml` & `MobileRead.cshtml`: moved scene version labels from main title area to left-hand navigation (desktop) and top nav metadata (mobile) for reduced reading noise (2026-04-21)
- [DONE] CHANGE-002 — `Views/Author/Publishing.cshtml`: align scene version labels beside scene titles using CSS Grid layout (2026-04-21)

---
## 3. Active Projects

### 3.1 RSprint — Passage Anchoring, Reader Continuity, and Inline Commentary

**Status:** 🔵 Planned — foundation capability

Establish a **core passage anchoring capability** that supports:

- Inline (selected text) comments  
- Cross-version comment relocation  
- Reader resume position across versions  
- Human correction (relink / reject)  
- Original context integrity  

This is a **platform capability**, not a feature.

**Sprint Series:**

- [DONE] **RS-A — Anchor Foundation**
  - [DONE] Phase A1 — Model discovery (Copilot-led inspection and proposal)
  - [DONE] Phase A2 — Domain definition (TDD)
  - [DONE] Phase A3 — Persistence (migration, additive only)
  - [DONE] Phase A4 — Application surface (creation/retrieval)

- [DONE] **RS-B — Anchored Resume**
  - [DONE] Phase B1 — Capture anchor from reading position
  - [DONE] Phase B2 — Restore using matching pipeline
  - [DONE] Phase B3 — Integration with ReadEvent
  - [DONE] Phase B4 — Tests (cross-version resume)

- [ ] **RS-C — Inline Comments**
  - [ ] Phase C1 — Selection capture
  - [ ] Phase C2 — Comment creation with anchor
  - [ ] Phase C3 — Rendering (inline indicators)
  - [ ] Phase C4 — Tests

- [ ] **RS-D — Deterministic Relocation**
  - [ ] Phase D1 — Exact matching
  - [ ] Phase D2 — Context matching
  - [ ] Phase D3 — Fuzzy matching
  - [ ] Phase D4 — Confidence scoring
  - [ ] Phase D5 — Integration and tests

- [ ] **RS-E — Human Override**
  - [ ] Phase E1 — Permission enforcement (reader + author only)
  - [ ] Phase E2 — Reject match (“wrong place”)
  - [ ] Phase E3 — Relink to new passage
  - [ ] Phase E4 — Status tracking (actor + timestamp)

- [ ] **RS-F — Original Context**
  - [ ] Phase F1 — Retrieve original version content
  - [ ] Phase F2 — Navigate to original anchor
  - [ ] Phase F3 — UI integration (“View original context”)

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

- [Started] **S-Sprint-1 — Foundation for background Dropbox sync**
  - [DONE] Phase 1: Architecture and task alignment
  - [DONE] Phase 2: Domain model for sync control
  - [DONE] Phase 3: Domain tests for control rules
  - [ ] Phase 4: Infrastructure mapping and migration
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
See `REFACTORING.md` for full detail.

- [DONE] Phase 1 — Centralise controller user/role resolution
- [ ] Phase 2 — Extract procedural controller workflows
- [ ] Phase 3 — Decompose startup/seeding
- [ ] Phase 4 — Standardise inheritance and shared utilities
- [ ] Phase 5 — Extract remaining procedural workflows
- [ ] Phase 6 — Standardise sync kickoff (remove inline `Task.Run`)

---

### 3.7 Post Go-Live Backlog

- Reader notification emails (new chapter published)
- Dropbox OAuth2 token refresh
- Dropbox webhook controller for push-based sync
- In-app Dropbox re-auth page
- Author/Comments view (mobile)
- Author Chapter Page (`Author/Chapter/{id}`)
- Publishing cascades (part-level, book-level)

---

## 4. Done

### Bugs Fixed

- [DONE] BUG-018 — Reader view did not display scene version number; DesktopRead and MobileRead now render a persistent scene version label from existing `CurrentVersionNumber` (`vN`) independent of update-banner state (2026-04-21)
- [DONE] BUG-017 — Sections view did not clearly surface pending synced scene changes; added explicit chapter-level “Pending changes” indication for published chapters with changed child scenes (2026-04-21)
- [DONE] BUG-016 — Publishing page leaked raw Razor token for version label; scene version hint now renders explicitly as text (e.g. `v3`) instead of showing `v@doc.CurrentVersionNumber` (2026-04-21)
- [DONE] BUG-001 — Reader removal not reflected in UI; repository now filters `!IsSoftDeleted` (2026-04-20)
- [DONE] BUG-003 — Settings surfaced ciphertext errors; now logs and redirects to error page instead of exposing exceptions (2026-04-21)
- [DONE] BUG-015 — Reader showed unpublished content and inconsistent banner version; now pinned to latest `SectionVersion` with stable versioned banner rendering (2026-04-21)
- [DONE] BUG-014 — Republishing a chapter created new versions for all scenes unconditionally; fixed to only create versions for scenes with `ContentChangedSincePublish = true` or no existing version (2026-04-20)
- [DONE] BUG-013 — Reader Account Settings missing font/size preferences; `AccountController.Settings` now uses `BaseController` role helpers to correctly identify BetaReader users (2026-04-20)
- [DONE] BUG-012 — New scene added in Scrivener did not trigger republish prompt; reconciliation now marks published parent chapter changed on new child scene creation (2026-04-20)
- [DONE] BUG-002 — System Support had no readers page; added `GET /Support/Readers` listing readers by display name and status only (2026-04-20)
- [DONE] BUG-007 — Activating a project now atomically deactivates the current active project (2026-04-20)
- [DONE] BUG-010 — Publishing page has no navigation link from Sections view or Dashboard
- [DONE] BUG-008 — Author/Section view had unreadable light-on-light prose and inconsistent visual design; removed inline styling, applied dark-theme token-based styling, and aligned breadcrumb/metadata/comments with author UI patterns (2026-04-20)
- [DONE] BUG-009 — New scene added in Scrivener did not appear after incremental sync; fixed by running `ReconcileProjectFromScrivxAsync` in the incremental path so new binder UUIDs are created from the cached local `.scrivx` without additional Dropbox API round-trips (2026-04-20)
- [DONE] BUG-006 — Unable to sync projects — seeder author lookup now Identity-ID-first; invalid ciphertext repaired on startup; duplicate author row repair added (2026-04-20)
- [DONE] BUG-005 — Password reset link immediately expired — reset flow now resolves Identity user by email fallback (2026-04-19)
- [DONE] BUG-004 — ForgotPassword returns HTTP 405 in production — two missing migrations applied; status code routing fixed
- [DONE] Cross-platform local cache path resolution — `IPlatformPathService`, platform-aware fallback (BugFix-Mac, 2026-04-19)
- [DONE] `/Author/InviteReader` submit production crash — operational failures route to `Home/Error` (BugFix-Mac, 2026-04-19)
- [DONE] MailKit NU1902 vulnerability — upgraded to 4.16.0 (BugFix-PC, 2026-04-19)
- [DONE] Reader view does not apply saved Reading Preferences (2026-04-17)
- [DONE] CS9107 in `AccountController` primary constructor (2026-04-17)
- [DONE] Reader/Read mobile view 404
- [DONE] Reader/Read comment box overflows page boundary on RHS
- [DONE] AddComment POST redirects to top of page — fixed with `#scene-{id}` anchors
- [DONE] Author/Dashboard Recent Activity truncation — replaced with persisted `AuthorNotification`
- [DONE] Login always redirected to Reader/Dashboard — fixed role-based redirect
- [DONE] Reader diff UX for removed paragraphs — thin markers instead of strikethrough

### Sprints Complete
- [DONE] V-Sprints 1–10 — Publishing and Versioning Series (636 tests). See `Publishing And Versioning Architecture.md`
- [DONE] Sprint 4 — Email Privacy and Controlled Access. See `Sprint4-EmailPrivacy.md`
- [DONE] Sprint 3 — Reader Font Preferences
- [DONE] Sprint 2 — Reader Experience
- [DONE] Sprint 1 — Pre-Beta Push
- [DONE] Email Sprint — Oracle Email Delivery, MailKit, DKIM, SPF
- [DONE] Role Migration — Identity roles, SystemSupport, SystemStateMessage, mobile reader flow
- [DONE] ScrivenerProject → Project rename
- [DONE] UserNotificationPreferences → UserPreferences rename
- [DONE] Incremental Refactor Phase 1
