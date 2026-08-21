# Sprint: System State Messaging — three-stage role migration plan

This document describes a three-stage approach to (A) migrate the codebase to use ASP.NET Identity role handling as the canonical authority for role membership and (B) use that role model consistently across Web, Application and Domain layers.

Goals:
- Replace scattered, domain-only role checks with ASP.NET Identity role checks and policy-based authorization.
- Move enforcement to appropriate layers (Web for UI gating; Application for service-level authorization) while keeping Domain focused on business invariants.
- Introduce `SystemSupport` role in Stage 3 and implement the System State Message feature with proper authorization.

Constraints / principles:
- Backwards compatible seeding and migration: existing domain `AppUsers` rows must remain valid during rollout.
- Prefer ASP.NET Core authorization primitives: `[Authorize(Roles = "...")]`, named policies, and `IAuthorizationService` for service-level checks.
- Add tests (xUnit + Moq) for each enforcement point.
- Use incremental, reversible changes per sprint stage.

---

## Stage 1 — Migrate Author and BetaReader checks to ASP.NET role handling (surface/web level)

Objective: Replace manual domain-role checks in controllers and views with standardized ASP.NET Identity roles and policies for `Author` and `BetaReader`. Keep domain `User.Role` for audit/read-only, and ensure identity/domain sync during seeding and registration.

Tasks (implementation and tests):

1. Inventory and tests
   - Create a test checklist enumerating every controller, view and helper that currently checks `User.Role` or queries `AppUsers.Role` (use code search results). Add unit tests demonstrating current behaviour where needed.

2. Identity role canonicalisation
   - Confirm `Role` enum names map exactly to Identity role names (`Author`, `BetaReader`).
   - Update `DatabaseSeeder` to ensure roles exist and to ensure any new Identity users are added to the appropriate Identity role.
   - Add a migration note / small script to backfill Identity role membership for existing users where IdentityUser exists (map by email) — create acceptance test for seeding.

3. Add authorization policies
   - In `AddIdentityServices` or `Program.cs` register named policies: `RequireAuthorPolicy`, `RequireBetaReaderPolicy` (policy that requires role membership via `RequireRole("Author")` etc.).
   - Unit tests: verify Policy registration and that `[Authorize(Policy = ...)]` would require role.

4. Controller refactor (surface level)
   - Replace manual `GetAuthorAsync()` / `RequireAuthorAsync()` usage by applying `[Authorize(Roles = "Author")]` or `[Authorize(Policy = "RequireAuthorPolicy")]` to controllers/actions where the whole controller or action is author-only.
   - For mixed controllers (some actions author-only), apply attribute at action level.
   - Remove redundant `Forbid()` checks in actions that become fully protected by attributes, keeping any additional domain preconditions.
   - Keep `BaseController.OnActionExecutionAsync` to populate `ViewBag` but read role membership from claims for performance (User.IsInRole or ClaimsPrincipal). Add unit tests for view bag population.

5. Views and UI
   - Replace checks that read `ViewBag.IsAuthor` / domain user role with `User.IsInRole("Author")` or keep `ViewBag` if it's still populated from claims. Ensure server-side hide/show logic uses Identity roles.
   - Update layout/footer scaffolding if needed.

6. Authentication/Registration flows
   - Ensure any user registration flows (if present) create a domain `User` and add the IdentityUser to the matching Identity role where appropriate. Add tests.

7. Tests & acceptance
   - Add controller/unit tests (xUnit + Moq) that assert unauthenticated or role-mismatched users are denied (403/redirect) for Author-only endpoints.
   - Manual QA checklist: sign in as seeded author, non-author, and ensure behaviour matches existing app.

Exit criteria for Stage 1:
- All web surface role checks use ASP.NET Identity roles/policies.
- Views show/hide UI using Identity role info (or a ViewBag populated from claims).
- Seeder backs Identity roles and maps existing users. Unit tests added.

Completed work (Stage 1 so far):
- Policies `RequireAuthorPolicy` and `RequireBetaReaderPolicy` have been added to DI via `AddIdentityServices`.
- A unit test project `DraftView.Web.Tests` and `AuthorizationPolicyRegistrationTests` were added to verify policy registration.
- `TASKS.md` updated to mark policy registration and test scaffolding as Done for Stage 1.
- `DatabaseSeeder` now ensures `BetaReader` role exists and backfills Identity role membership for existing domain users by email (idempotent; logs failures).
- `AuthorController` and `DropboxController` are decorated with `[Authorize(Policy = "RequireAuthorPolicy")]`.
- `BaseController` now populates `ViewBag.IsAuthor` and `ViewBag.IsReader` using claim-based `User.IsInRole(...)`, falling back to domain lookup when necessary.

Next Stage 1 tasks (remaining):
- Inventory and replace remaining manual controller guards with policy attributes or policy checks (work in progress).
- Update views to use `User.IsInRole` or rely on the claim-populated `ViewBag` (individual view updates remain).
- Add controller integration tests asserting unauthorised access is blocked (xUnit + Moq).

---

## Stage 2 — Enforce roles in the Application layer (service-level authorization)

Objective: Move sensitive authorization logic into the Application layer so services enforce role-based constraints, preventing bypass by non-web callers. Use ASP.NET Core authorization primitives (policies, IAuthorizationService) or a small `IAuthorizationFacade` to avoid pulling HttpContext into services.

Tasks (implementation and tests):

1. Design authorization integration for services
   - Add `IAuthorizationFacade` (or use `IAuthorizationService`) abstraction injectable into application services that need authorization.
   - Provide a concrete implementation that uses `IHttpContextAccessor` to resolve ClaimsPrincipal and check roles, but design the interface so it can be mocked and used by non-HTTP callers (e.g., background jobs) by passing a `ClaimsPrincipal` or `UserContext`.

2. Identify critical service methods
   - Audit application services for operations requiring Author role (e.g., publish/unpublish, manage readers, project management) and BetaReader-specific operations.
   - Create an itemised list of public methods to require service-level authorization.

3. Implement service-level checks
   - Inject `IAuthorizationFacade` into selected services and perform role checks at method entry. When authorization fails, throw a domain-specific `UnauthorisedOperationException` or return failure result as appropriate.
   - Prefer policy names (e.g., `RequireAuthorPolicy`) rather than role strings to keep rules single-sourced.

4. Update callers
   - Update Web layer callers to pass CurrentUserId (and optionally a ClaimsPrincipal) into services where required. Keep Web layer attributes for UI gating, but do not rely solely on them.

5. Tests
   - Add unit tests (xUnit + Moq) for services asserting that unauthorized callers are rejected and authorized callers succeed; include cases where the caller provides an id that is not in the `Author` role.
   - Add integration-style tests if feasible to validate both Web and Application enforcement together.

6. Background jobs / non-web callers
   - Define how background services obtain an identity (service account or impersonation) and ensure role checks still work (e.g., map service principal to Author where required or allow explicit privileged background operations through config).

Exit criteria for Stage 2:
- Application services enforce role checks and cannot be bypassed by directly calling service methods.
- Tests cover authorized and unauthorized service calls.

---

## Stage 3 — Implement SystemSupport role and System State Messaging with ASP.NET role enforcement

Objective: Add `SystemSupport` as an ASP.NET Identity role, wire up Web UI and Application services to require that role for managing system state messages, and implement the System State Message feature per the original spec.

Tasks (implementation and tests):

1. Identity and seeding
   - Ensure `SystemSupport` Identity role exists via seeder.
   - Seed `support@draftview.co.uk` to Identity role and create/match a domain `User` with `Role.SystemSupport` if needed for audit. Provide a migration/backfill script.

2. Domain model & rules
   - Implement `SystemStateMessage` aggregate/entity (fields and rules as previously specified), using the chosen audit model (create new row on edit, revoke old row).
   - Add repository methods: get active, get history (latest-first), get by id.

3. Application services
   - Add `ISystemStateMessageService` with methods: Create, Edit, Revoke, GetActive, GetHistory.
   - Service implementations must enforce `SystemSupport` authorization via `IAuthorizationFacade` / policies.
   - Ensure creating a new message revokes previous active in same transaction.

4. Web layer
   - Add `SupportController` protected with `[Authorize(Roles = "SystemSupport")]` and UI pages for create/edit/revoke/history.
   - Add footer integration: layout fetches active message read-only (safe to fail: log + ignore) and renders it.

5. Infrastructure
   - EF mapping and migration for `SystemStateMessage` and indexes on `Revoked`, `ExpiresAtUtc`, `CreatedAtUtc`.

6. Tests
   - Domain, application, and infrastructure tests (xUnit + Moq) per earlier minimum behaviours.
   - Controller tests asserting only support users can access support endpoints.

7. Rollout and checklist
   - Run data migration/backfill to sync Identity membership for support user.
   - Smoke test UI and footer.

Exit criteria for Stage 3:
- SystemSupport role exists and is enforced across Web and Application layers.
- System State Messaging feature implemented with audit/history and protected UI.

---

## Cross-stage concerns and non-functional tasks

- Documentation: update developer docs indicating Identity role is canonical, how to seed roles, and how to write service-level authorization checks.
- Monitoring / logging: log failed authorization attempts and provide a dev debug mode to visualise role mappings.
- Migration safety: ensure seeding is idempotent and provide rollback steps.
- Tests: use xUnit + Moq; add integration tests where services and policies are exercised together.
- Backwards compatibility: keep domain `User.Role` readable for a transitional period, but add a TODO to remove duplicate role storage after rollout.

---

## Delivery plan & milestones

- Week 1: Stage 1 (web surface migration, seeding, and tests)
- Week 2: Stage 2 (application-layer enforcement, facade design, service tests)
- Week 3: Stage 3 (SystemSupport role, System State Messaging feature, infra/migrations, tests)

Note: adjust sprint length based on team capacity. Each stage contains unit and integration tests — do not merge without test coverage for enforcement points.

---

## Acceptance criteria (overall)

- Identity roles are the single source of truth for authorization decisions.
- Web controllers use `[Authorize]` attributes and/or policies for UI gating.
- Application services validate role membership and cannot be bypassed by direct calls.
- SystemSupport is implemented and secures System State Messaging management endpoints.
- Tests (xUnit + Moq) cover positive and negative authorization paths.