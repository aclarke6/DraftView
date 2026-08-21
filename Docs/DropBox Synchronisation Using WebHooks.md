# DraftView Platform Architecture — DropBox Synchronisation Using WebHooks

---

See DraftView Platform Architecture — Publishing and Versioning for the platform pattern, phased delivery style, and deployable-slice rule. This document defines a second synchronisation path for Dropbox that runs in the background and updates working state only. It does not publish content, create versions, or change reader-visible state.

## Revision History

| Version | Change |
|---------|--------|
| v1.0 | Initial architecture for webhook-driven background Dropbox synchronisation |
| v1.1 | Added S-Sprint-8: separate daily health check and reconciliation app |

---

## 1. Purpose

Define how DraftView can use Dropbox webhooks and controlled background processing to keep synced projects fresh without relying only on author-initiated sync.

This document covers:

- The purpose of webhook-driven background sync
- The architectural boundaries between the existing web app and the background sync path
- The control model for request, hold, lease, and retry
- A phased implementation plan
- A sprint structure that can be issued to Copilot one phase at a time

This mechanism is an ingestion concern only.

It updates local synced copies and then runs the existing sync pipeline so that DraftView working state stays current.

It does **not**:

- create `SectionVersion` records
- publish anything to readers
- change versioning behaviour
- bypass author-controlled publishing

---

## 2. Core Principles

> Webhook sync is background ingestion only.
> 
> Webhook sync never publishes.
> 
> Webhook sync never creates versions.
> 
> The existing app remains the owner of publishing and reader state.
> 
> Dropbox events request work. They do not perform heavy work inline.
> 
> Freshness should improve without creating burst load or duplicate sync work.

---

## 3. Architectural Summary

DraftView already has a sync pathway that downloads Dropbox project files and updates working state. The webhook design does not replace that pathway. It adds a second trigger path for the same underlying sync behaviour.

There are now two forms of synchronisation:

1. **Foreground / existing sync**
   - Existing application flow
   - Used when the author or system explicitly invokes sync
   - Runs through the current Dropbox and Scrivener sync pipeline

2. **Background / webhook sync**
   - Triggered by Dropbox account change notifications
   - Schedules controlled background work
   - Reuses the same downstream sync behaviour
   - Updates working state only

The background design must be deliberately modest.

It is not a new publishing subsystem. It is not a new reader feature. It is not a replacement for the platform layer.

It is a second ingress path into synchronisation.

---

## 4. Relationship to Existing Architecture

In the Publishing and Versioning architecture, DraftView distinguishes between the **ingestion layer** and the **platform layer**. Sync belongs to the ingestion layer, and sync never creates versions. That invariant remains unchanged here.

Webhook-driven background sync therefore sits alongside the current Dropbox / Scrivener sync implementation as another way of initiating ingestion work, while all published-state behaviour remains in the platform layer.

The design intent is:

- keep the existing `ISyncProvider` abstraction intact where possible
- keep `ScrivenerSyncService` as the sync implementation of record
- keep publishing, versioning, comments, read events, and reader behaviour unchanged
- add background sync as a controlled operational concern, not as a product-level semantic change

---

## 5. High-Level Flow

```text
Dropbox file change
    ↓
Dropbox webhook hits DraftView endpoint
    ↓
Webhook handler validates request and records sync demand
    ↓
Short-lived background job or durable request processor wakes
    ↓
Processor checks lease and hold state
    ↓
If held: exit and leave request for background worker
    ↓
If not held: acquire lease
    ↓
Interrogate Dropbox using saved cursor
    ↓
If relevant project files changed: run existing sync pipeline
    ↓
Update cursor, sync timestamps, and hold window
    ↓
Release lease
```

This design gives fast reaction without heavy inline webhook work.

---

## 6. The Simple Control Model

The control model is intentionally small.

### 6.1 Sync request
A webhook or fallback poll marks that a sync is required.

### 6.2 Hold
A project that has just synced should not be re-synced immediately. A cooldown window prevents repeated work on the same project during bursts.

### 6.3 Lease
Only one worker may actively process sync for a project at a time.

### 6.4 Background pickup
If a request arrives during the hold window, the immediate processor exits. A periodic worker later picks up the request once the hold window has expired.

This gives:

- debouncing
- burst protection
- controlled load
- a small operational model that fits the current application scale

---

## 7. State Model

The simplest durable state for background sync is per synced project.

### 7.1 Required control fields

| Property | Description |
|----------|-------------|
| `LastSuccessfulSyncUtc` | Timestamp of the most recent successful background or foreground sync |
| `LastSyncAttemptUtc` | Timestamp of the most recent attempted sync |
| `SyncRequestedUtc` | Timestamp of the most recent durable request for sync |
| `HeldUntilUtc` | Cooldown boundary. If in the future, immediate processing must not sync yet |
| `SyncLeaseId` | Nullable unique lease token for the active sync processor |
| `SyncLeaseExpiresUtc` | Lease expiry in case a worker crashes |
| `DropboxCursor` | Saved Dropbox cursor for change interrogation |
| `LastWebhookUtc` | Most recent webhook touch for audit and diagnostics |
| `LastBackgroundSyncOutcome` | Optional status summary for diagnostics |

### 7.2 Derived operational states

A project can be thought of as being in one of four operational states:

- **Idle** — no outstanding request, not held, not leased
- **Requested** — sync requested and eligible when worker picks it up
- **Held** — sync requested but within cooldown window
- **Syncing** — lease active and work in progress

These states can be derived from the stored fields. They do not require a separate status enum unless needed for diagnostics.

---

## 8. Webhook Design

### 8.1 One app-level webhook endpoint
The system should expose a single Dropbox webhook endpoint for the DraftView Dropbox app.

The endpoint does not perform sync itself.

It should only:

- validate the webhook handshake and signature
- extract changed Dropbox account identifiers (logged but not used for filtering in single-author mode)
- record durable sync demand for all Scrivener projects
- return success quickly

### 8.2 No per-author webhook endpoint
Webhook routing should happen inside DraftView after receipt. The system should not create a dedicated webhook endpoint per author.

### 8.3 Request recording behaviour (single-author mode)
When a webhook is received, the handler records sync demand for **all** `ScrivenerDropbox` projects:

- `SyncRequestedUtc = now`
- `LastWebhookUtc = now`

Since only one author exists in the system, all Scrivener projects are potentially affected by any Dropbox webhook notification. The background sync orchestrator will use per-project cursors to determine whether each specific project's folder actually changed.

No heavy sync, download, or parse work belongs in the webhook action itself.

---

## 9. Immediate Processor Behaviour

The webhook may trigger a short-lived independent process path to check whether immediate execution is allowed.

That processor should:

1. load project sync control state
2. stop if an active lease exists and is still valid
3. stop if `HeldUntilUtc > now`
4. acquire a lease if processing may proceed
5. interrogate Dropbox using the stored cursor
6. if relevant files changed, run the existing sync pipeline
7. update timestamps, cursor, and cooldown
8. release the lease

If the processor stops because the project is held, it does not discard the request. The background worker remains responsible for picking it up later.

---

## 10. Background Worker Behaviour

A periodic worker polls the database for projects that still require sync and are now eligible.

The worker should process only projects where:

- `SyncRequestedUtc` indicates outstanding demand
- no valid lease exists
- `HeldUntilUtc <= now`

The worker operates in bounded batches.

This keeps system load predictable and avoids burst fan-out from noisy webhook traffic.

### 10.1 Why the worker remains necessary
The background worker is the safety net for:

- held requests
- dropped immediate jobs
- temporary Dropbox or network failure
- lease expiry recovery
- daily freshness reconciliation

---

## 11. Cursor-Based Dropbox Interrogation

Webhook notification alone is not enough to know which project file changed.

The application must use a saved Dropbox cursor to determine what has changed and whether those changes affect the tracked project folder.

### 11.1 Per-project cursor storage (single-author mode)
Each `ScrivenerDropbox` project stores its own cursor. This allows independent tracking of changes within each project's Dropbox folder path.

The flow is:

- load project's stored cursor
- request changed entries from Dropbox using that cursor
- filter changes to the project's `DropboxPath`
- if relevant changes found, download changed entries and run the existing update path
- store the updated cursor on the project regardless of whether changes were relevant

This preserves a lightweight ingestion model and allows per-project sync control.

### 11.2 Single-author simplification
In single-author mode, when a webhook arrives, all Scrivener projects are marked as `SyncRequestedUtc`. Each project's background sync execution independently interrogates Dropbox using its own cursor. Projects whose folders were not affected will update their cursor but skip the sync pipeline.

---

## 12. Reuse of Existing Components

The current code already contains the main building blocks for incremental Dropbox interrogation and download:

- `DropboxFileDownloader.ListChangedEntriesAsync(...)`
- `DropboxFileDownloader.ListAllEntriesWithCursorAsync(...)`
- `DropboxFileDownloader.DownloadChangedEntriesAsync(...)`
- `DropboxClientFactory.CreateForUserAsync(...)`
- the existing sync pipeline that updates working state after local file download

That means the background sync design should prefer orchestration around the current infrastructure rather than replacing it.

The first implementation should focus on:

- when to invoke sync
- how to debounce sync
- how to persist sync control state
- how to reuse the current download and parse path safely

It should not redesign the Dropbox integration from scratch.

---

## 13. Suggested Service Responsibilities

### 13.1 Webhook controller or endpoint
Single responsibility: receive and validate Dropbox webhook requests, then record sync demand.

### 13.2 Background sync request service
Single responsibility: create or update durable sync request state for affected projects.

### 13.3 Sync lease service
Single responsibility: acquire, verify, extend if needed, and release the project sync lease.

### 13.4 Background sync orchestration service
Single responsibility: decide whether a project is held, eligible, or ready; interrogate Dropbox; invoke the current sync path; update control fields.

### 13.5 Periodic reconciliation worker
Single responsibility: poll for eligible requested or stale projects and process them in bounded batches.

### 13.6 Existing sync provider
Single responsibility: continue to download, parse, and update working state. No publishing responsibility is added.

---

## 14. Behaviour Rules

### 14.1 Webhook rule
A webhook hit records demand. It does not perform heavyweight sync inline.

### 14.2 Cooldown rule
If a project has synced recently, a further request within the cooldown window must not trigger immediate sync.

### 14.3 Lease rule
Only one active sync processor may operate on a project at any time.

### 14.4 Hold persistence rule
A held project still retains outstanding sync demand. Hold delays work. It does not discard work.

### 14.5 Background recovery rule
The periodic worker must later process any held or missed request that becomes eligible.

### 14.6 Publishing boundary rule
Background sync may update working state only. It must not create versions or reader-visible changes.

### 14.7 Failure rule
On failure, the request remains recoverable. Lease expiry and future worker passes must allow safe retry.

---

## 15. Fallback Polling

Webhooks improve freshness but should not be the sole mechanism.

A slower periodic poll remains useful for:

- long periods without webhook delivery
- operational drift
- cursor expiry recovery
- stale projects not touched recently
- production resilience

The fallback poll should be modest. It is not intended to compete with webhook responsiveness.

A practical model is:

- frequent worker pass for pending held requests
- daily stale reconciliation pass for all eligible synced projects

---

## 16. Security and Operational Notes

- Webhook requests must be signature-validated
- Webhook endpoint must return quickly
- Sync state changes must be durable before acknowledging work completion internally
- Lease expiry must protect against abandoned workers
- Diagnostic logging must capture webhook receipt, lease acquisition, hold refusal, sync start, sync completion, sync failure, and retry behaviour
- Webhook retries and duplicate notifications must be assumed normal

---

## 17. System Behaviour Summary

| Event | Action |
|-------|--------|
| Dropbox sends webhook | Record sync demand for affected project or projects |
| Project is currently held | Immediate processor exits; request remains outstanding |
| Project is currently syncing | Immediate processor exits; active lease remains authoritative |
| Project is eligible for immediate sync | Acquire lease, interrogate Dropbox, run existing sync if relevant |
| Dropbox change irrelevant to tracked folder | Update cursor, leave working state unchanged |
| Background worker finds held request now eligible | Acquire lease and process sync |
| Sync succeeds | Update timestamps, cursor, hold window, release lease |
| Sync fails | Record failure, release or expire lease, leave request recoverable |
| Daily stale reconciliation runs | Request or process sync for overdue projects |

---

## 18. Key Constraints

- Background webhook sync is ingestion only
- It must not publish content
- It must not create `SectionVersion` records
- It must not alter reader-visible behaviour directly
- It must reuse the existing sync pipeline where possible
- Webhook work must be lightweight and fast to acknowledge
- Duplicate webhook notifications are expected
- Hold and lease must be separate concepts
- A held request must remain recoverable
- The worker must operate in bounded batches

---

## 19. Known Debt and Deliberate Deferrals

- **Single-author webhook mapping:** Current implementation marks all `ScrivenerDropbox` projects for sync on webhook receipt. Multi-tenant filtering via `Project.DropboxAccountId` deferred to MT-Sprint-1.
- **Per-project cursor storage:** Each project stores its own Dropbox cursor. Per-account cursor consolidation (optimization to reduce Dropbox API calls) deferred until multi-project performance becomes an issue.
- Account-level batching across many projects may be introduced later if required
- More advanced priority logic is deferred
- Queue infrastructure is deferred
- Distributed worker coordination beyond DB lease control is deferred
- Separate sync service deployment is deferred
- Rich sync diagnostics UI is deferred
- Non-Dropbox providers are out of scope in this document

---

## 21. S-Sprint-8 — Daily Health Check and Reconciliation App

### 21.1 Purpose
A separate console application that runs once per day to detect and recover from operational issues that webhook sync cannot handle:
- Projects with no successful sync in 24+ hours (stale projects)
- Projects with expired or missing Dropbox cursors
- Projects with abandoned leases (expired but not cleared)

This is a safety net. Webhook sync (S-Sprint-1 through S-Sprint-7) handles normal operation. The health check app recovers from edge cases.

### 21.2 Separate Application Architecture
The health check is deployed as a standalone console app, not built into the web application.

**Why separate:**
- Deployment independence (web app restarts don't interrupt health checks)
- Operational isolation (health check failures don't cascade to web app)
- Resource allocation (can run on lower-priority schedule/VM)
- Clear execution boundaries (daily cron job, not long-running background service)

**Deployment:**
- Compiled as `DraftView.HealthCheck.dll`
- Deployed to same VM as web app (e.g., `/opt/draftview/health-check/`)
- Scheduled via systemd timer (Linux) or Task Scheduler (Windows)
- Runs once per day at 3:00 AM UTC (low-traffic period)

**Shared infrastructure:**
- References `DraftView.Domain`, `DraftView.Application`, `DraftView.Infrastructure`
- Reuses existing repositories, Dropbox services, sync pipeline
- No code duplication—orchestration only

### 21.3 Lease-Based Mutual Exclusion
**Critical rule:** HealthCheck must acquire a lease before processing any project.

**Why lease (not hold):**
- Lease provides mutual exclusion (prevents webhook worker from syncing same project)
- Hold is post-success cooldown (not pre-work protection)
- Lease expiry protects against HealthCheck crashes (webhook can retry after 30 minutes)

**HealthCheck lease behavior:**
```csharp
// Acquire lease with 30-minute timeout (longer than webhook's 5 minutes)
var leaseAcquired = await _leaseService.TryAcquireLeaseAsync(
    project.Id, 
    leaseId, 
    TimeSpan.FromMinutes(30), 
    ct);

if (!leaseAcquired)
{
    // Webhook worker or another HealthCheck already processing—skip
    return;
}

try
{
    // Perform reconciliation work
    await ReconcileProjectAsync(project, ct);

    // On success: set normal 2-minute cooldown hold
    project.HeldUntilUtc = DateTime.UtcNow.AddMinutes(2);
}
finally
{
    // Always release lease
    await _leaseService.ReleaseLeaseAsync(project.Id, leaseId, ct);
}
```

### 21.4 Stale Project Threshold
**Configurable via `appsettings.json`:**
```json
{
  "HealthCheck": {
    "StaleProjectThresholdHours": 24,
    "MaxProjectsPerRun": 50
  }
}
```

**Query for stale projects:**
```sql
SELECT * FROM Projects 
WHERE ProjectType = 0 -- ScrivenerDropbox
  AND LastSuccessfulSyncUtc < (NOW() - INTERVAL '24 hours')
  AND (SyncLeaseId IS NULL OR SyncLeaseExpiresUtc < NOW())
LIMIT 50;
```

### 21.5 Cursor Health and Full Rescan
**Normal flow:**
1. Try cursor-based sync first (call `ListChangedEntriesAsync` with stored cursor)
2. If successful: process incremental changes via existing sync pipeline
3. Update cursor and timestamps

**Escalation to full rescan:**
Triggered when:
- `DropboxCursor` is null (missing cursor)
- Dropbox API returns `"reset"` or `"expired_cursor"` error

**Full rescan behavior:**
1. Query all Dropbox entries (no cursor): `ListAllEntriesAsync(project.DropboxPath)`
2. Compare against local cache to identify changed files
3. Download **only changed files** (not re-download everything)
4. Run existing sync pipeline (reconciliation, parse, update working state)
5. Store new cursor from Dropbox response
6. Never create versions or trigger publishing (ingestion-only)

### 21.6 Abandoned Lease Cleanup
**Definition:** A lease is abandoned when `SyncLeaseId` is set but `SyncLeaseExpiresUtc < now`.

**HealthCheck behavior:**
```csharp
if (project.SyncLeaseId != null && project.SyncLeaseExpiresUtc < DateTime.UtcNow)
{
    _logger.LogWarning("Clearing abandoned lease on project {ProjectId}", project.Id);

    project.SyncLeaseId = null;
    project.SyncLeaseExpiresUtc = null;

    // Only mark for retry if project is genuinely stale
    if (project.LastSuccessfulSyncUtc < DateTime.UtcNow.AddHours(-24))
    {
        project.SyncRequestedUtc = DateTime.UtcNow;
    }

    await _unitOfWork.SaveChangesAsync(ct);
}
```

**HealthCheck does not attempt sync after clearing lease**—it leaves work for webhook worker or next HealthCheck run.

### 21.7 Operational Constraints
- Bounded batch: process maximum 50 projects per run (prevent runaway workload)
- Lease timeout: 30 minutes (longer than webhook's 5 minutes to allow full rescan)
- Cooldown after success: 2 minutes (same as webhook sync)
- Logging: structured logs for all interventions (cleared leases, full rescans, cursor errors)

### 21.8 Success Criteria
- Stale projects recovered within 24 hours
- Expired cursors detected and rebuilt
- Abandoned leases cleared automatically
- Zero impact on webhook sync operation
- Zero version creation or publishing

---

## 22. Multi-Tenancy Upgrade Requirements (Future)

When multi-tenancy is implemented (MT-Sprint-1), the following changes will be required to this webhook sync design:

### 20.1 Domain changes
- Add `Project.DropboxAccountId` field to map projects to Dropbox accounts
- Populate `DropboxAccountId` during Dropbox OAuth connection flow
- Store Dropbox account ID per tenancy (likely via new `DropboxConnection` entity linking `TenancyId` to `DropboxAccountId`)

### 20.2 Webhook handler changes
- Filter projects by `DropboxAccountId` when recording sync demand
- Only mark projects belonging to the webhook's changed Dropbox account

### 20.3 Cursor optimization opportunity
- Optionally consolidate to per-account cursor storage (instead of per-project)
- Query Dropbox once per account, distribute changes to multiple projects
- Reduces Dropbox API calls for authors with multiple projects

### 20.4 Migration path
- Schema migration: add nullable `DropboxAccountId` to `Projects` table
- Backfill existing projects with their connected Dropbox account ID
- Update webhook request recording service to use filtered query

All sync control logic (lease, hold, cursor interrogation, sync execution) remains unchanged. Multi-tenancy affects only the webhook-to-project mapping step.

---

## 23. Implementation Plan

The implementation should proceed in thin deployable slices.

### Step 1 — Persist sync control state
Add the minimum durable fields required to request, hold, and lease background sync.

### Step 2 — Receive webhook safely
Expose a validated Dropbox webhook endpoint that records demand and returns quickly.

### Step 3 — Add orchestration service
Introduce a background sync orchestration service that checks hold and lease rules and reuses existing Dropbox incremental sync helpers.

### Step 4 — Add periodic worker
Poll the database for eligible held or stale requests and process them safely.

### Step 5 — Harden operations
Add diagnostics, retry behaviour, and manual verification of the complete sync request lifecycle.

---

# S-Sprint 1 — Foundation for Background Dropbox Sync

## Goal

Create the minimal durable state and orchestration seam required for webhook-driven sync without changing visible author or reader behaviour.

## Phase 1 — Architecture and task alignment
**Brief:** Update `TASKS.md` and any architecture tracking documents to introduce background Dropbox webhook sync as an ingestion-only feature with explicit non-goals around publishing and reader state.

## Phase 2 — Domain model for sync control
**Brief:** Add the domain-level sync control properties or entity needed to represent sync request time, cooldown hold, active lease, cursor storage, and last sync timestamps for a synced project.

## Phase 3 — Domain tests for control rules
**Brief:** Add TDD coverage for the control model, especially rules for held requests, lease ownership, lease expiry, and durable outstanding demand.

## Phase 4 — Infrastructure mapping and migration
**Brief:** Persist the new background sync control state in EF Core, add configuration and repository support, and create the migration without exposing any user-visible behaviour.

## Deployable state
- Database can store webhook sync control state
- No webhook endpoint yet
- No visible behaviour change

---

# S-Sprint 2 — Webhook Receipt and Durable Request Recording

## Goal

Accept Dropbox webhook notifications and record background sync demand safely and quickly.

## Phase 1 — Webhook endpoint surface
**Brief:** Add a single Dropbox webhook endpoint in the existing web application with handshake support and request validation scaffolding.

## Phase 2 — Signature validation and request parsing
**Brief:** Implement Dropbox webhook signature validation and parsing of changed account identifiers, keeping the controller or endpoint thin.

## Phase 3 — Request recording service
**Brief:** Introduce an application service that records `SyncRequestedUtc` and `LastWebhookUtc` for all `ScrivenerDropbox` projects when a webhook is received (single-author mode).

## Phase 4 — Web endpoint tests
**Brief:** Add tests proving the webhook endpoint validates correctly, records sync demand, remains lightweight, and does not perform heavyweight sync work inline.

## Deployable state
- Dropbox can notify DraftView
- Requests are stored durably
- No actual background sync execution yet

---

# S-Sprint 3 — Immediate Orchestration Path

## Goal

Allow a webhook-triggered job to decide whether immediate sync can proceed or must be deferred.

## Phase 1 — Sync lease service
**Brief:** Create the service that acquires, verifies, and releases a project sync lease with expiry support.

## Phase 2 — Cooldown hold evaluation
**Brief:** Add the control logic that prevents immediate sync when `HeldUntilUtc` is still in force while preserving outstanding demand.

## Phase 3 — Background sync orchestration service shell
**Brief:** Create the orchestration service that loads control state, checks hold and lease rules, and decides whether to exit or proceed.

## Phase 4 — Orchestration tests
**Brief:** Add tests proving that duplicate requests collapse safely, held projects are deferred, leased projects are skipped, and eligible projects proceed.

## Deployable state
- Safe decision layer exists
- Still no Dropbox delta interrogation yet

---

# S-Sprint 4 — Dropbox Delta Interrogation and Incremental Download

## Goal

Reuse the existing Dropbox incremental helpers to determine whether a webhook-triggered change affects a tracked project and, if so, download only the changed files.

## Phase 1 — Cursor integration
**Brief:** Wire the orchestration service to use stored Dropbox cursors and update them after change interrogation.

## Phase 2 — Relevant-path filtering
**Brief:** Add logic that filters Dropbox changed entries to the tracked project path so irrelevant account changes do not trigger full project sync.

## Phase 3 — Incremental download integration
**Brief:** Reuse the existing changed-entry download path to fetch only relevant changed files into the existing local cache structure.

## Phase 4 — Dropbox delta tests
**Brief:** Add tests proving cursor progression, relevant-path filtering, and changed-entry download behaviour without changing publishing rules.

## Deployable state
- Background path can identify and download relevant changes
- Working state update path still not fully invoked end to end

---

# S-Sprint 5 — Reuse Existing Sync Pipeline End to End

## Goal

Connect the webhook-driven background path to the current sync pipeline so the local download updates DraftView working state exactly as existing sync does.

## Phase 1 — Existing pipeline integration seam
**Brief:** Introduce the application seam that allows the background orchestrator to invoke the current sync pipeline without duplicating parsing or working-state update logic.

## Phase 2 — End-to-end background sync execution
**Brief:** When relevant Dropbox changes are detected and the lease is held, run the existing sync pipeline, update sync timestamps, and set the cooldown hold on successful completion.

## Phase 3 — Failure and recovery handling
**Brief:** Ensure failures do not lose outstanding demand, leases expire safely, and retries remain possible through later worker passes.

## Phase 4 — Integration tests
**Brief:** Add tests proving that webhook-triggered background sync updates working state only and does not publish or create versions.

## Deployable state
- Webhook-triggered sync works end to end
- Publishing behaviour remains untouched

---

# S-Sprint 6 — Periodic Worker and Held Request Recovery

## Goal

Add the periodic database-polled worker that processes held, missed, or stale requests in bounded batches.

## Phase 1 — Worker host and scheduling
**Brief:** Add a background hosted worker that periodically polls for eligible requested projects that are no longer held and not actively leased.

## Phase 2 — Batch selection and bounded processing
**Brief:** Implement bounded query selection and safe loop processing so worker load remains predictable.

## Phase 3 — Held request recovery
**Brief:** Ensure that requests deferred by `HeldUntilUtc` are picked up automatically once eligible.

## Phase 4 — Worker tests
**Brief:** Add tests proving worker eligibility rules, bounded batch behaviour, held request recovery, and respect for active leases.

## Deployable state
- Held and missed work is recovered automatically
- System no longer relies on the immediate path alone

---

# S-Sprint 7 — Stale Reconciliation and Operational Hardening

## Goal

Complete the system with stale-project reconciliation, diagnostics, and production-safe observability.

## Phase 1 — Daily stale reconciliation
**Brief:** Add the slower periodic stale-sync path that requests background sync for overdue synced projects even without recent webhook traffic.

## Phase 2 — Diagnostics and audit logging
**Brief:** Add structured logging and diagnostic state for webhook receipt, request creation, hold refusal, lease acquisition, sync execution, and failure outcomes.

## Phase 3 — Manual operational controls
**Brief:** Add minimal operational controls or diagnostics needed for support and troubleshooting, without building a large admin UI.

## Phase 4 — Browser and operational verification
**Brief:** Complete end-to-end verification of webhook receipt, background sync execution, held-request delay, lease protection, and stale reconciliation in a non-production environment.

## Deployable state
- Background webhook sync is operationally viable
- Reconciliation and diagnostics are in place

---

# S-Sprint-8 — Daily Health Check and Reconciliation App

## Goal

Deploy a separate console application that runs daily to detect and recover from operational issues that webhook sync cannot handle.

## Phase 1 — Separate console app scaffolding
**Brief:** Create `DraftView.HealthCheck` console project with DI setup (DbContext, repositories, services), logging configuration, and deployment scripts (systemd service or Task Scheduler config).

## Phase 2 — Stale project reconciliation with lease-based protection
**Brief:** Implement stale project detection (no successful sync in 24+ hours), lease acquisition with 30-minute timeout, cursor-based sync attempt, and escalation to full rescan on cursor expiry.

## Phase 3 — Cursor health and abandoned lease cleanup
**Brief:** Add detection and recovery for missing/expired Dropbox cursors (trigger full rescan) and abandoned leases (clear lease, mark for retry if stale).

## Phase 4 — Full rescan orchestration and operational verification
**Brief:** Implement full rescan path (query all Dropbox entries without cursor, download only changed files), test on projects with broken cursors, verify no version creation or publishing, and confirm lease mutual exclusion with webhook workers.

## Deployable state
- Health check app deployed as systemd timer (Linux) or Task Scheduler (Windows)
- Runs daily at 3:00 AM UTC
- Stale projects recovered within 24 hours
- Expired cursors rebuilt
- Abandoned leases cleared
- Zero impact on webhook sync operation

---

# Sprint Order Summary

| Sprint | Goal |
|-------|------|
| S-Sprint 1 | Foundation for background Dropbox sync |
| S-Sprint 2 | Webhook receipt and durable request recording |
| S-Sprint 3 | Immediate orchestration path |
| S-Sprint 4 | Dropbox delta interrogation and incremental download |
| S-Sprint 5 | Reuse existing sync pipeline end to end |
| S-Sprint 6 | Periodic worker and held request recovery |
| S-Sprint 7 | Stale reconciliation and operational hardening |

---

# Phased Delivery Rules

Every phase across every sprint must satisfy all of the following before deployment:

1. All new tests are green
2. No existing tests have regressed
3. The system remains in a complete, non-broken state
4. No phase introduces publishing or reader-visible behaviour by accident
5. TASKS.md is updated
6. Changes are committed with a meaningful message

> Build thin, complete slices.
> 
> Reuse the existing sync path where possible.
> 
> Keep webhook work fast.
> 
> Hold delays work. It does not discard work.
> 
> Background sync is ingestion only.
