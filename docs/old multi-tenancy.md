# DraftView — Old Multi-Tenancy Notes
Date: 2026-08-19

This document records earlier multi-tenancy ideas and assumptions that were reviewed during consolidation but were **not** carried forward into `MultiTenancy.md` as the current implementation plan.

---

## Discarded or superseded positions

### 1. Treating multi-tenancy as a post-revenue concern

Discarded. `TASKS.md` now marks multi-tenancy as high priority because additional author demand has made it a near-term requirement.

### 2. Treating one proposal document as fully authoritative

Discarded. The current implementation plan is a synthesis of the business model, billing model, task list, webhook design notes, and repository agent rules.

### 3. Keeping `MultiTenancy.md` as an outline-only document

Discarded. The active document now contains sprint-by-sprint requirements, deliverables, and exit criteria.

### 4. Centralising AGENTS.md requirements in one generic section for this workstream

Discarded. The current plan embeds the relevant AGENTS requirements inside each sprint so the execution constraints travel with the work.

### 5. Using `ReaderTenant` as the target model

Discarded. `ReaderTenant` remains a historical stepping-stone idea only. The target model is `Account` + `Tenancy` + `TenancyMembership`.

### 6. Making MT-Sprint-5 part of the near-term implementation boundary

Discarded. Reader Marketplace remains a post-revenue sprint and is not required to make the platform safely multi-tenant.

### 7. Waiting until the product is live with a single author before planning the tenancy split

Discarded. That assumption no longer matches current roadmap pressure or the task list priority.

### 8. Keeping RSprint completion as a hard prerequisite for MT-Sprint-1

Discarded. Reader-experience work remains valuable, but the updated multi-tenancy plan is driven by author onboarding, reader identity, and tenancy isolation needs.

---

## Ideas retained elsewhere

The following ideas were **kept**, but moved into the new consolidated plan rather than left as separate proposal fragments:

- `Account`, `Tenancy`, `TenancyMembership`, and `TenancySubscription`
- tenancy-owned Dropbox connections
- `IBillingProvider` abstraction
- cross-tenancy reader identity
- tenancy-scoped webhook and sync follow-up work
- `AuthorId` as the interim tenancy anchor until MT-Sprint-1
