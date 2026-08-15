# DraftView — Task List
Last updated: 2026-08-15

---

## 0. Summary

**Live at:** https://draftview.co.uk
**Production:** Oracle Cloud VM `141.147.71.62`, .NET 10, PostgreSQL, Nginx, Cloudflare SSL
**Repository:** https://github.com/aclarke6/DraftView

### Current Test State
- 860 total, 859 passed, 1 skipped, 0 failed
- 1 skipped — `SmtpEmailSenderIntegrationTests` (sends real email, manual only)

### Active Work
| Track | Status |
|-------|--------|
| RSprint Series | 🟡 In progress — RS-A to RS-E complete, RS-F next |
| S-Sprint Series | 🟡 In progress — S-Sprint-1 complete, S-Sprint-2 next |
| MT-Sprint Series | 🔵 Pre-planning — see `MultiTenancy.md` |
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

- [ ] CHANGE-003 — `Views/Reader/DesktopRead.cshtml`: allow the left and right reader panels to collapse and expand for a wider reading surface

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

### 3.7 Post Go-Live Backlog

- Reader notification emails (new chapter published)
- Dropbox OAuth2 token refresh
- Dropbox webhook controller for push-based sync
- In-app Dropbox re-auth page
- Author/Comments view (mobile)
- Author Chapter Page (`Author/Chapter/{id}`)
- Publishing cascades (part-level, book-level)
