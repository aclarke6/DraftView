# DraftView — Multi-Tenancy Sprint Series
Version: 0.1 | Date: 2026-04-20
Status: **Pre-planning** — not yet scheduled

---

## Overview

Multi-tenancy is the architectural evolution that transforms DraftView from a single-author platform into a platform that supports many independent authors, each with their own readers, projects, and data isolation. It is a prerequisite for public launch, billing, and the Reader Marketplace.

This document collects all multi-tenancy related work, design decisions, and sprint planning. It is the authoritative reference for this work stream.

**Reference documents:**
- `ScrivenerSync-BusinessModel-v3.docx` — full domain model, invariants, service definitions
- `ScrivenerSync-BillingModel-v2.docx` — subscription tiers, billing abstraction
- `TASKS.md` — points here for all multi-tenancy work (section 3.4)

---

## Prerequisites (must be complete before MT-Sprint-1)

- [ ] Billing abstraction layer in place (`IBillingProvider`) — Creem preferred at launch, Paddle as second implementation
- [ ] Production is stable and go-live prerequisites in TASKS.md section 4 are complete
- [ ] RSprint-1 complete (reader and author experience improvements)

---

## Current State (Single-Tenant)

The platform currently operates as a single-tenant system:

- One `User` entity covers both Author and BetaReader roles via a `Role` property
- `AuthorId` is used on author-scoped tables as an interim tenancy scope
- One `DropboxConnection` per author
- One active project per author at a time (BUG-007)
- Sync interval is global (5 minutes, hardcoded)
- No `Tenancy`, `TenancyMembership`, or `Account` entities exist

**Interim rule (enforced now):** Every new author-scoped table gets `AuthorId`. When multi-tenancy arrives, `AuthorId` becomes `TenancyId` and the migration is mechanical.

---

## Target Architecture (from BusinessModel-v3)

### Core Entities
| Entity | Purpose |
|--------|---------|
| `Account` | Platform-level identity. Owns login credentials and DisplayName. May hold memberships across many Tenancies. |
| `Tenancy` | One per author. Owns projects, readers, and subscription. |
| `TenancyMembership` | Links an Account to a Tenancy with a Role (Author or BetaReader). |
| `TenancySubscription` | One per Tenancy. Tracks subscription tier and limits. |
| `DropboxConnection` | One per Tenancy (not per Account). |

### Key Invariants
- Exactly one `TenancyMembership` with Role = Author per Tenancy
- An Account may own at most one Tenancy
- An Account may hold BetaReader memberships in any number of Tenancies
- Each Tenancy has exactly one `DropboxConnection`
- Each Tenancy has exactly one `TenancySubscription`
- No data belonging to one Tenancy is accessible to another

### Subscription Tiers
| Tier | Beta Readers | Active Projects |
|------|-------------|-----------------|
| Free | 3 | 1 |
| Paid | 10 | Unlimited |
| Ultimate | Unlimited | Unlimited |

---

## Migration Strategy

### Phase 1 — Account / Tenancy Split
Rename and restructure the existing `User` entity into `Account` + `TenancyMembership`. This is the largest structural change and must be done as a single atomic migration.

Key changes:
- `AppUsers` → `Accounts` (platform identity, no Role column)
- New `Tenancies` table
- New `TenancyMemberships` table (Role, IsActive, IsSoftDeleted, etc.)
- `AuthorId` columns → `TenancyId` columns (mechanical rename, same values)
- `DropboxConnections.UserId` → `DropboxConnections.TenancyId`
- `UserPreferences` splits: author digest prefs → `TenancyMembership`, reader font prefs → `Account`

### Phase 2 — Subscription Enforcement
- Add `TenancySubscription` entity and migration
- Enforce reader count and active project count limits at the application layer
- `IBillingProvider` abstraction — Creem implementation first
- Developer-managed tier assignment in Phase 1 (no self-serve billing UI yet)

### Phase 3 — Author Registration Flow
- Self-serve author sign-up creates Account + Tenancy + Author TenancyMembership atomically
- Replaces the current seeder-driven author creation
- Dropbox OAuth connect flow scoped to Tenancy

### Phase 4 — Reader Cross-Tenancy Identity
- A reader Account can hold memberships in multiple Tenancies
- Invitation flow creates TenancyMembership against existing Account if email matches
- DisplayName owned by Account, propagates across all Tenancies
- Soft-delete of Account makes all TenancyMemberships inert

### Phase 5 — Reader Marketplace (post-revenue)
- Cross-tenancy reader discovery
- Public reader profiles
- Reputation system (deferred — design TBD)

---

## Items Migrated from TASKS.md

The following items from TASKS.md section 3.4 are now owned by this document:

- [ ] `IBillingProvider` abstraction (Creem preferred, Paddle alternative)
- [ ] Subscription tiers: Free / Paid / Ultimate
- [ ] `ReaderTenant` model (`AuthorId`, `IsActive`, `IsDeleted`, `KnownAs`) — superseded by `TenancyMembership` in target architecture
- [ ] Reader Marketplace (post-revenue)

### Items from other sections that have multi-tenancy implications:

- [ ] **BUG-007** — One active project per author invariant becomes one active project per Tenancy
- [ ] **Sync interval** — currently hardcoded at 5 minutes globally; should become a per-Tenancy configuration
- [ ] **Dropbox OAuth token refresh** — currently manual; scoped to Tenancy in target architecture
- [ ] **Dropbox webhook** — push-based sync; scoped to Tenancy
- [ ] **`SyncBackgroundService`** — currently iterates all projects globally; must become Tenancy-scoped
- [ ] **`GetAllBetaReadersAsync()`** — known debt, must be replaced with Tenancy-scoped query before MT-Sprint-1
- [ ] **`RepairDuplicateAuthorRowsAsync`** — startup repair method; can be removed once Account/Tenancy split is complete and the PENDING ciphertext root cause is fixed
- [ ] **Author registration** — currently seeder-only; self-serve flow required before public launch

---

## Sprint Plan (outline — detail TBD)

| Sprint | Name | Key Deliverable |
|--------|------|----------------|
| MT-Sprint-1 | Account/Tenancy Split | `Account`, `Tenancy`, `TenancyMembership` entities; migration from `AppUsers`; all existing functionality preserved |
| MT-Sprint-2 | Subscription Enforcement | `TenancySubscription`, `IBillingProvider`, Creem integration, tier limits enforced |
| MT-Sprint-3 | Author Registration | Self-serve sign-up flow, Dropbox connect scoped to Tenancy |
| MT-Sprint-4 | Reader Cross-Tenancy | Multi-tenancy reader invitations, DisplayName propagation, Account soft-delete |
| MT-Sprint-5 | Reader Marketplace | Post-revenue, design TBD |

---

## Design Decisions (to be resolved before MT-Sprint-1)

- [ ] **Email encryption scope** — `EmailCiphertext` currently per-platform. In multi-tenancy, does each Tenancy get its own encryption key, or is one platform key sufficient? Decision affects key management and GDPR isolation.
- [ ] **`UserPreferences` split** — author digest preferences belong to TenancyMembership; reader font preferences belong to Account. Migration path needed.
- [ ] **`AuthorNotification` scope** — currently `AuthorId`; becomes `TenancyId`. Existing notifications must be migrated.
- [ ] **`SystemStateMessage`** — currently platform-wide. In multi-tenancy, should messages be platform-wide or per-Tenancy? Likely platform-wide for operational messages.
- [ ] **`ReaderAccess` table** — may be superseded entirely by `TenancyMembership`. Audit before MT-Sprint-1.
- [ ] **Sync background service identity** — currently runs as trusted system actor. In multi-tenancy, must be explicitly scoped per Tenancy to prevent cross-tenancy data access.

---

## Notes

- Do not build the full Tenancy/TenancyMembership entity model until billing is in place and the product is live with a single author.
- The interim `AuthorId` strategy ensures no structural rework is needed — the refactor is mechanical when the time comes.
- MT-Sprint-1 is a breaking migration. It must be planned as a single deployment with no intermediate state where the schema is partially migrated.
