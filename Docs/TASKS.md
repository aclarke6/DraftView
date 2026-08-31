# DraftView — Task List
Last updated: 2026-08-31
Last deployed: 2026-08-31 13:20 (commit: 708b929)
Last merged: 2026-08-31 — PR #105 (fix: CHANGE-024 GetChapterChangeStatusesAsync null baseline) merged to main

---

## Summary

**Live at:** https://draftview.co.uk
**Production:** Oracle Cloud VM `141.147.71.62`, .NET 10, PostgreSQL, Nginx, Cloudflare SSL
**Repository:** https://github.com/aclarke6/DraftView

### Current Test State
- 1,360 total, 1,360 passed, 1 skipped, 0 failed
- 1 skipped — `SmtpEmailSenderIntegrationTests` (sends real email, manual only)

---

## Reference Documents

| Document | Purpose |
|----------|---------|
| `AGENTS.md` | Authoritative execution rules for all coding agents |
| `DraftView Git Rules.md` | Branching strategy, issue workflow, merge gates, commit standards |
| `HISTORY.md` | Completed sprints, changes, and bugs — full audit trail |
| `PRINCIPLES.md` | Core engineering principles — architecture, layering, behavioural rules |
| `REFACTORING.md` | Refactoring roadmap and constraints |
| `PowerShell.md` | PowerShell scripting standards |
| `MultiTenancy.md` | Multi-tenancy design decisions and migration strategy |
| `Passage Anchoring, Reader Continuity, and Inline Commentary.md` | RSprint series design |
| `DropBox Synchronisation Using WebHooks.md` | S-Sprint series architecture |
| `Publishing And Versioning Architecture.md` | SectionVersion, publish/republish rules |
| `DraftView-UAT-Plan.md` | UAT plan and validation scenarios |

---

## How Work Is Tracked

- **Bugs** — GitHub Issues, labelled "Claude CLI" if Claude should fix
- **Changes and Sprints** — GitHub Issues, labelled "Claude CLI" if Claude should implement
- **TASKS.md** — priority list only; one line per item pointing at the issue
- **Completed work** — closed on GitHub; summary in `HISTORY.md`
- **Recurring failures** — reopen the original issue rather than creating a new one

---

## Priority Work

### P1 — Blocking or Urgent

| # | Item | Issue | Status |
|---|------|-------|--------|
| 1 | Apply 4 pending EF migrations to production | [#61](https://github.com/aclarke6/DraftView/issues/61) | Reopen when publish runs — script now fixed |
| 2 | `Account.Activate()` not called on email confirmation | [#63](https://github.com/aclarke6/DraftView/issues/63) | Open — blocks /Join going live |
| 3 | Resolve 3 build warnings | [#59](https://github.com/aclarke6/DraftView/issues/59) | PR #62 open |
| 4 | UAT: complete scenarios C-K | — | In progress |
| 5 | Go-Live Day: password reset emails to Becca and Hilary | — | Blocked by UAT |

### P2 — Active Sprint Work

| # | Item | Issue | Status |
|---|------|-------|--------|
| 1 | MT-Sprint local phase: AuthorId to TenancyId rename and data backfill | [#69](https://github.com/aclarke6/DraftView/issues/69) | Open — prerequisite for RD-Sprint-1 |
| 2 | RS-F: Original Context | [#65](https://github.com/aclarke6/DraftView/issues/65) | Open |
| 3 | RD-Sprint-1: Reader Dashboard shell | [#68](https://github.com/aclarke6/DraftView/issues/68) | Open — depends on #69 |
| 4 | S-Sprint-2: Webhook receipt and durable request recording | [#67](https://github.com/aclarke6/DraftView/issues/67) | Open |
| 5 | Notification chapter context | [PR #60](https://github.com/aclarke6/DraftView/pull/60) | Open — awaiting merge |

### P3 — Platform Hardening

| # | Item | Issue | Status |
|---|------|-------|--------|
| 1 | Report Fault modal | [#70](https://github.com/aclarke6/DraftView/issues/70) | Open |
| 2 | SystemStateMessage expiry | [#71](https://github.com/aclarke6/DraftView/issues/71) | Open |
| 3 | Log failed authorisation attempts | [#72](https://github.com/aclarke6/DraftView/issues/72) | Open |

### P4 — Changes and Features

| # | Item | Issue | Status |
|---|------|-------|--------|
| 1 | CHANGE-006: Collapsible reader nav + panel pin/unpin | [#64](https://github.com/aclarke6/DraftView/issues/64) | Open |
| 2 | CHANGE-011: Panoramic banner asset (~2200x700 px) | [#54](https://github.com/aclarke6/DraftView/issues/54) | Open — asset needed |
| 3 | RS-H: Reader Insight | [#66](https://github.com/aclarke6/DraftView/issues/66) | Open — follows RS-F |

---

## Active Sprint Series — Status

| Series | Next Up | Issue | Design Doc |
|--------|---------|-------|------------|
| RSprint (Passage Anchoring) | RS-F | [#65](https://github.com/aclarke6/DraftView/issues/65) | `Passage Anchoring...md` |
| S-Sprint (Dropbox Webhooks) | S-Sprint-2 | [#67](https://github.com/aclarke6/DraftView/issues/67) | `DropBox Synchronisation...md` |
| MT-Sprint (Multi-Tenancy) | Local phase | [#69](https://github.com/aclarke6/DraftView/issues/69) | `MultiTenancy.md` |
| RD-Sprint (Reader Dashboard) | RD-Sprint-1 | [#68](https://github.com/aclarke6/DraftView/issues/68) | See HISTORY.md |

---

## Backlog (Post Go-Live)

- Ubuntu OS upgrade: 20.04 to 22.04 to 24.04 (pg_dump first)
- Reader notification emails (new chapter published)
- Dropbox OAuth2 token refresh and in-app re-auth page
- Author/Comments view (mobile)
- Author Chapter Page (`Author/Chapter/{id}`)
- Publishing cascades (part-level, book-level)
- S-Sprints 3-8 (see `DropBox Synchronisation Using WebHooks.md`)
- RS-H: Reader Insight ([#66](https://github.com/aclarke6/DraftView/issues/66))
- MT-Sprint-5: Reader Marketplace (post-revenue)
- Refactoring Phase 3-5 (see `REFACTORING.md`)
