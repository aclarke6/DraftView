# DraftView — Multi-Tenancy Sprint Series
Version: 1.0 | Date: 2026-08-19
Status: **Planned for implementation** — MT-Sprint-1 through MT-Sprint-4 are the active delivery roadmap; MT-Sprint-5 remains post-revenue.

---

## 1. Purpose

This document consolidates the best viable ideas from the existing multi-tenancy proposals and repository planning documents into a single implementation reference for DraftView.

It supersedes the earlier outline-only version of this file. Ideas that were considered and deliberately not carried forward are recorded in `/docs/old multi-tenancy.md`.

---

## 2. Source Material Consolidated Here

This plan is synthesized from the following repository sources rather than treating any single proposal as fully authoritative:

- `DraftView Business Model v3.docx`
- `DraftView Billing Model v2.docx`
- `TASKS.md` section 3.4
- `DropBox Synchronisation Using WebHooks.md` section 22
- `AGENTS.md`
- `.github/copilot-instructions.md`
- `Principles.md`

---

## 3. Confirmed Planning Decisions

### 3.1 Core delivery scope

Multi-tenancy is now a near-term platform requirement because:

- multiple authors must be supported,
- readers must be able to read across authors,
- an author may also act as a beta reader in another workspace,
- Reader Dashboard work depends on cross-author identity and access modelling.

### 3.2 Implementation sprint boundary

**Decision:** MT-Sprint-1 through MT-Sprint-4 are the implementation sprints for core multi-tenancy.

**Decision:** MT-Sprint-5 remains a documented **post-revenue** sprint.

Rationale:

- MT-Sprint-1 through MT-Sprint-4 are required to support multiple authors safely.
- Reader Marketplace is not required to make tenancy isolation, reader access, billing, or author self-service work.
- Keeping MT-Sprint-5 deferred prevents discovery, profile, and reputation work from diluting the high-risk structural migration work.

### 3.3 Repository rule for current code before MT-Sprint-1 lands

Until MT-Sprint-1 is implemented:

- every new author-scoped entity must continue to carry `AuthorId`,
- `AuthorId` remains the tenancy anchor,
- no new global unscoped queries may be introduced.

### 3.4 Billing rollout decision

- Billing is deferred until after go-live.
- No billing provider has been selected yet.
- Until billing is implemented, every author tenancy operates on Free Tier semantics.
- Authors may be shown higher tiers and offered the ability to request a tier change, but the product must clearly state that higher tiers are not implemented yet.
- The tenancy carries a non-user-editable maximum beta-reader count property.
- That property is initially set to **5** readers for all tenancies before billing is live.
- When payment implementation is introduced, the Free Tier beta-reader limit changes to **3**.

---

## 4. Current State and Required Change

The current product is still structurally single-tenant:

- `User` currently represents both author and beta-reader identity,
- `AuthorId` is used as an interim tenancy scope,
- reader access is still centred on project-level access,
- background sync assumptions are global in places,
- Dropbox linkage is not yet tenancy-aware,
- Reader Dashboard and future discovery features need cross-author reader identity.

The target state is:

- `Account` becomes the platform identity,
- `Tenancy` becomes the author-owned workspace boundary,
- `TenancyMembership` becomes the link between an `Account` and a `Tenancy`,
- `TenancySubscription` becomes the billing and tier enforcement boundary,
- Dropbox connections, sync orchestration, reader access, and notifications become tenancy-scoped.

---

## 5. Target Model

### 5.1 Core entities

| Entity | Purpose |
|--------|---------|
| `Account` | Platform identity, login credential owner, display-name owner, may participate in multiple tenancies |
| `Tenancy` | Author-owned workspace containing projects, readers, notifications, integrations, and operational limit properties such as maximum beta-reader count |
| `TenancyMembership` | Role-bearing link between `Account` and `Tenancy` |
| `TenancySubscription` | Tier state, future billing-provider identifiers, and subscription status |
| `DropboxConnection` | Dropbox binding owned by a `Tenancy`, not by a person-account |

### 5.2 Core invariants

- Exactly one author membership exists per tenancy.
- An account may own at most one tenancy.
- An account may hold reader memberships in many tenancies.
- No data from one tenancy may be visible to another unless explicitly represented by a membership in both tenancies.
- Soft delete remains the default deletion model.
- Historical and published content remains immutable under the existing versioning rules.

---

## 6. Cross-Cutting Requirements That Shape Every Sprint

- Maintain architecture boundaries: Domain → Application → Infrastructure → Web.
- Domain, Application, and Infrastructure work follows the AGENTS.md TDD sequence.
- Web changes remain thin HTTP wiring only: resolve identity, validate, call service, map result, return response.
- Every persistence change ships with its EF Core migration in the same batch.
- Rename migrations must preserve data; generated `DropTable/CreateTable` pairs must be reviewed and rewritten where preservation is required.
- No global unscoped author/reader queries may survive into MT-Sprint-1 completion.
- If a sprint touches publishing/versioning flows, the implementation must also follow `.github/Instructions/versioning.instructions.md`.

---

## 7. Sprint Delivery Plan

### MT-Sprint-1 — Account / Tenancy / TenancyMembership split

### Goal

Replace the single `User` identity model with the minimum viable tenancy model while preserving all current author and reader behaviour.

### Required deliverables

- `Account`, `Tenancy`, and `TenancyMembership` entities
- migration from `AppUsers` to the new tenancy-aware structure
- `AuthorId` → `TenancyId` rename on author-scoped tables where the new model is introduced
- `DropboxConnections.UserId` → `DropboxConnections.TenancyId`
- tenancy-safe repository queries and application service boundaries
- removal or replacement of known unscoped queries such as `GetAllBetaReadersAsync()`

### Specific requirements for this sprint

- Preserve existing data through an atomic migration; no partial live state is acceptable.
- Treat rename completeness as mandatory across entities, repositories, DTOs, view models, controllers, tests, and migrations.
- Replace project-global or platform-global author lookups with tenancy-scoped lookups.
- Audit `ReaderAccess` and decide whether it remains temporarily or is subsumed by `TenancyMembership`.
- Audit `AuthorNotification`, `UserPreferences`, and other current `AuthorId` tables as part of the tenancy-key migration.

### AGENTS.md requirements embedded in this sprint

- Domain/Application/Infrastructure changes require TDD: stub, failing test, green implementation, then full relevant verification.
- Migration and behaviour changes must not be split across unrelated commits.
- Controllers must not perform repository access or orchestration while tenancy wiring is introduced.
- Every new tenancy-scoped entity must still respect soft-delete and factory-method rules.
- This sprint must stop immediately if data preservation would require an unsafe intermediate schema state.

### Exit criteria

- Existing single-author data migrates into the new model without loss.
- One author workspace maps cleanly to one tenancy.
- A reader identity can now be represented independently from authorship.
- No remaining repository method returns author/reader data without an explicit scope.

---

### MT-Sprint-2 — Subscription enforcement and billing abstraction

### Goal

Introduce tenancy-level subscription state and enforce product limits without leaking provider-specific logic into the wrong layers, while keeping payment activation deferred until after go-live.

### Required deliverables

- `TenancySubscription` entity
- tenancy-owned maximum beta-reader count property
- `IBillingProvider` abstraction
- provider selection deferred until a later billing implementation decision
- tier limit enforcement for beta readers and active projects
- pre-billing handling that places all authors on Free Tier semantics

### Subscription tiers carried forward

Operational note before payment is live:

- every tenancy starts with `MaxBetaReaderCount = 5`,
- the property is not user-editable,
- once payment is implemented, the Free Tier reader cap changes to 3 and higher tiers can be activated properly.

| Tier | Beta Readers | Active Projects |
|------|--------------|-----------------|
| Free | 3 | 1 |
| Paid | 10 | Unlimited |
| Ultimate | Unlimited | Unlimited |

### Specific requirements for this sprint

- Billing provider concerns remain in Infrastructure behind `IBillingProvider`.
- Domain and Application layers may depend on abstractions and billing state, but not provider SDK logic.
- Lapse behaviour must favour reader continuity and avoid public-facing disruption where possible.
- Tenancy, not account, is the billing boundary.
- No provider-specific choice is assumed in this document; selection is deferred.
- Higher tiers may be described and requested before implementation, but the request path must explicitly state that paid tier behaviour is not live yet.
- The initial operational control is tenancy-owned `MaxBetaReaderCount`, seeded to 5 for all author tenancies before billing launch.

### AGENTS.md requirements embedded in this sprint

- TDD remains mandatory for all new domain rules and application enforcement paths.
- No DbContext access outside Infrastructure.
- No controller-level billing orchestration.
- Every schema change ships with the feature and its migration together.
- If a safe implementation cannot be validated within the allowed cloud-phase test scope, the sprint must stop for a local validation phase.

### Exit criteria

- Subscription state is persisted per tenancy.
- Maximum beta-reader count is enforced from a tenancy-owned property rather than a user-editable setting.
- Tier checks execute in application services.
- Provider choice is replaceable by configuration.
- Active-project and reader-count limits can be enforced without special-case controller logic.

---

### MT-Sprint-3 — Author self-serve registration and tenancy bootstrap

### Goal

Allow a new author to create an account and tenancy without manual seeding, and connect Dropbox at the tenancy level.

### Required deliverables

- self-serve author registration flow
- atomic creation of `Account` + `Tenancy` + author `TenancyMembership`
- tenancy-scoped Dropbox connect flow
- removal of author-only seeding as the required path for onboarding

### Specific requirements for this sprint

- Registration must fail as a unit if any required record in the bootstrap chain fails.
- Dropbox connection ownership must be unambiguously tenancy-scoped.
- Startup assumptions and repair logic that only exist to support the old author bootstrap path must be reviewed and removed where safe.

### AGENTS.md requirements embedded in this sprint

- Web controllers stay thin and only delegate to application services.
- Multi-step registration orchestration belongs in Application, not Web.
- Infrastructure owns OAuth, persistence, and external integration details.
- No domain logic may be hidden inside repository implementations or controller branching.

### Exit criteria

- A new author can register without manual seeding.
- Each new author receives exactly one tenancy.
- Dropbox can be connected without relying on person-scoped ownership.

---

### MT-Sprint-4 — Reader cross-tenancy identity

### Goal

Allow a single reader account to participate in multiple author tenancies while preserving tenancy isolation.

### Required deliverables

- invitation flow that attaches to an existing `Account` when the email already exists
- support for one account holding memberships in multiple tenancies
- account-owned `DisplayName`
- safe account soft-delete behaviour that inactivates memberships without cross-tenant leakage
- repository and service support for reader dashboard queries that span memberships safely

### Specific requirements for this sprint

- Readers must be able to access books from more than one author without duplicate login identities.
- Authors who are also readers must be representable without role collision.
- Reader Dashboard dependencies such as "books available" and "discover authors" must query through memberships rather than project-only access records.
- `IProjectRepository.GetAllForReaderAsync(Guid userId)` or equivalent tenancy-safe query support must exist by sprint completion.

### AGENTS.md requirements embedded in this sprint

- No global reader query may bypass tenancy or membership scope.
- Application services own role-switching and invitation orchestration.
- Web must not branch on business rules for invitation acceptance or dashboard composition.
- Soft delete remains the deletion strategy for user-visible identity state.

### Exit criteria

- One account can read across multiple authors.
- One account can be both author and reader in different tenancy contexts.
- Reader-facing queries are membership-aware and tenancy-safe.

---

### MT-Sprint-5 — Reader Marketplace (deferred post-revenue)

### Decision

This sprint remains in the roadmap, but it is **not** part of the near-term implementation required to make DraftView multi-tenant.

### Why it is deferred

- Marketplace discovery is a growth feature, not a tenancy-foundation feature.
- It depends on the identity, membership, and billing work completed in MT-Sprint-1 through MT-Sprint-4.
- Deferring it reduces migration risk and keeps the immediate roadmap focused on data isolation and operational safety.

### Placeholder scope

- discovery of authors open to readers
- public or semi-public reader profiles
- reputation/trust mechanisms
- tenancy-safe discovery filtering and opt-in controls

### AGENTS.md requirements embedded in this sprint

- Marketplace work must not begin by weakening tenancy isolation rules already established.
- Discovery queries must remain explicitly scoped and opt-in.
- Any new public-facing data exposure requires the same layered architecture and TDD discipline as earlier sprints.

---

## 8. Other Repository Work That Must Be Pulled Into the Sprint Plan

- **BUG-007** — one active project per author becomes one active project per tenancy
- **Dropbox webhook upgrade** — webhook-to-project mapping must become tenancy-aware using Dropbox account linkage
- **`SyncBackgroundService`** — global iteration must become tenancy-scoped
- **Sync interval** — global 5-minute assumption should become tenancy-owned configuration
- **Dropbox token refresh** — must become tenancy-owned operational state
- **Author notifications** — current `AuthorId` scope migrates to tenancy scope
- **User preferences split** — author digest preferences move to membership scope; reader font preferences remain account scope

---

## 9. Open Design Decisions To Resolve Before MT-Sprint-1 Starts

- Whether `ReaderAccess` remains as a transitional table or is fully superseded by `TenancyMembership`
- Whether encryption remains platform-keyed or becomes tenancy-keyed for protected email data
- Exact migration path for `UserPreferences`
- Final tenancy-safe operating model for sync background workers and trusted system actions
- Exact backfill strategy for Dropbox account identifiers when webhook filtering becomes tenancy-aware

---

## 10. Definition of Done for the Multi-Tenancy Workstream

The core multi-tenancy workstream is complete when:

- MT-Sprint-1 through MT-Sprint-4 are delivered,
- all author and reader data access is tenancy-safe,
- billing is enforced per tenancy,
- authors self-register without manual seeding,
- readers can participate across multiple authors with a single account,
- no remaining single-tenant assumptions block Reader Dashboard or future marketplace work.
