# DraftView - Passage Anchoring, Reader Continuity, and Inline Commentary

**Date:** 2026-04-22
**Status:** Planned architecture and execution contract
**Execution model:** Incremental, test-driven, production-safe at every sprint boundary
**Scope:** RS-A through RS-H

---

## 0. Purpose

This document is the source of truth for the RSprint series.

The RSprint series introduces a platform capability for anchoring reader-facing
positions and comments to passages of published content. It supports:

- Inline comments on selected text
- Reader resume position across edited versions
- Cross-version relocation of comments and resume points
- Human correction when automated relocation is wrong
- Original context retrieval for trust and auditability
- Author insight into reader progress

This is a platform capability, not a single UI feature. Every implementation phase
must preserve existing reader behavior, existing comments, and the versioning
architecture.

---

## 1. Non-Negotiable Rules

### 1.1 Versioning Boundary

Reader-facing anchors are resolved against published content.

- If a `SectionVersion` exists, anchors must reference and resolve against
  `SectionVersion.HtmlContent`.
- `Section.HtmlContent` is working state and must not become the authoritative
  reader-facing anchor source when a `SectionVersion` exists.
- The existing pre-versioning fallback to `Section.HtmlContent` may be supported only
  for legacy sections with no `SectionVersion`.
- Sync and import code must not create, relocate, or mutate anchors.
- `SectionVersion.HtmlContent` remains immutable.

### 1.2 Layering Boundary

The existing layered architecture remains mandatory:

- Domain owns anchor entities, value objects, invariants, and state transitions.
- Application owns use cases, orchestration, authorization checks, and DTOs.
- Infrastructure owns EF Core mappings, repositories, and external integrations.
- Web owns HTTP, Razor rendering, JavaScript capture, input validation, and ViewModels.

The Web layer must never decide relocation confidence, mutate anchor state directly,
or call repositories directly.

### 1.3 Trust Boundary

The system must never imply certainty when the match is uncertain.

- Exact matches may be presented as located.
- Context/fuzzy/AI matches must carry visible confidence metadata when surfaced.
- Orphaned anchors remain visible in the comment/progress surface, clearly marked as
  not currently located.
- Human relink outranks every automated match.
- Human rejection suppresses the rejected automated location until a newer explicit
  relink or a new relocation attempt on a later content version.

### 1.4 Data Safety

- Original anchor data is immutable after creation.
- Derived match data is replaceable.
- No comments are deleted because an anchor cannot be located.
- No reader progress is deleted because an anchor cannot be located.
- Existing comments without anchors remain valid.
- Migrations must be additive and backward compatible.

---

## 2. Vocabulary

| Term | Meaning |
|------|---------|
| Anchor | Durable reference to a selected passage or reading position. |
| Original anchor | Immutable capture of the source passage and context at creation time. |
| Resolved anchor | Current best location of an anchor in a target content version. |
| Relocation | Process of finding an original anchor in a later content version. |
| Orphaned anchor | Anchor with no trustworthy current location. |
| Relink | Human-selected replacement location for an anchor. |
| Rejection | Human statement that a proposed current location is wrong. |
| Canonical text | Text extracted from HTML using deterministic normalization rules. |
| Selector | Data needed to locate a passage in canonical text and optionally in rendered HTML. |

---

## 3. Anchor Model Contract

### 3.1 Core Domain Concepts

RS-A Phase A1 selected the following names and responsibilities for the current codebase:

| Concept | Layer | Purpose |
|---------|-------|---------|
| `PassageAnchor` | Domain entity | Owns original immutable anchor capture and current status. |
| `PassageAnchorSnapshot` | Domain value object | Immutable source text, context, offsets, and hashes. |
| `PassageAnchorMatch` | Domain value object | Current resolved location, confidence, source, and target version. |
| `PassageAnchorStatus` | Domain enum | Current state visible to Application and Web. |
| `PassageAnchorPurpose` | Domain enum | Distinguishes comment, resume, progress, and future uses. |
| `PassageAnchorMatchMethod` | Domain enum | Exact, context, fuzzy, AI, manual relink, rejected, orphaned. |

These names are now the implementation contract for RS-A A2-A4 unless a later phase hits
a stop condition and records a replacement decision before code changes continue.

### 3.1.1 RS-A Phase A1 Implementation Decisions

RS-A Phase A1 confirmed that the current model can support an additive anchor migration.
The implementation must use these exact file/class decisions for A2-A4:

Domain files:

- `DraftView.Domain/Entities/PassageAnchor.cs`
- `DraftView.Domain/ValueObjects/PassageAnchorSnapshot.cs`
- `DraftView.Domain/ValueObjects/PassageAnchorMatch.cs`
- `DraftView.Domain/Enumerations/PassageAnchorPurpose.cs`
- `DraftView.Domain/Enumerations/PassageAnchorStatus.cs`
- `DraftView.Domain/Enumerations/PassageAnchorMatchMethod.cs`
- `DraftView.Domain/Interfaces/Repositories/IPassageAnchorRepository.cs`

Domain modifications:

- `DraftView.Domain/Entities/Comment.cs` gets nullable `Guid? PassageAnchorId`.
- `DraftView.Domain/Entities/ReadEvent.cs` gets nullable `Guid? ResumeAnchorId`.
- Existing `Comment` factories must keep working with a null anchor.
- Existing `ReadEvent` creation and update paths must keep working with a null resume anchor.

Infrastructure files:

- `DraftView.Infrastructure/Persistence/Configurations/PassageAnchorConfiguration.cs`
- `DraftView.Infrastructure/Persistence/Repositories/PassageAnchorRepository.cs`
- `DraftView.Infrastructure/Persistence/DraftViewDbContext.cs`
- `DraftView.Infrastructure/Persistence/Configurations/CommentConfiguration.cs`
- `DraftView.Infrastructure/Persistence/Configurations/ReadEventConfiguration.cs`
- an additive EF migration for `PassageAnchors`, `Comments.PassageAnchorId`, and
  `ReadEvents.ResumeAnchorId`

Application files:

- `DraftView.Application/Services/PassageAnchorService.cs`
- `DraftView.Application/Contracts/CreatePassageAnchorRequest.cs`
- `DraftView.Application/Contracts/PassageAnchorDto.cs`
- `DraftView.Application/Contracts/PassageAnchorSnapshotDto.cs`
- `DraftView.Application/Contracts/PassageAnchorMatchDto.cs`
- `DraftView.Domain/Interfaces/Services/IPassageAnchorService.cs`
- `DraftView.Web/Extensions/ServiceCollectionExtensions.cs` for DI registration only

Reader progress decision:

- `ReadEvent` is sufficient for RS-A and RS-B latest-position resume anchoring.
- Store the active resume anchor in `ReadEvent.ResumeAnchorId`.
- Do not introduce a separate progress-history table in RS-A.
- A separate historical progress/event table may be considered later only if RS-H or
  analytics requirements need position history beyond the latest resume point.

Branching decision:

- The earlier `RS-A-base/phase-*` branch pattern is not Git-valid when `RS-A-base` exists
  as a branch.
- RS-A implementation phases must use `RS-A/base` and `RS-A/phase-*` branches.
- Phase branches merge `RS-A/phase-*` -> `RS-A/base` -> `main`.

### 3.2 PassageAnchor Required Data

A persisted anchor must be able to answer:

- Which section is this anchor associated with?
- Which original content version was used when the anchor was created?
- What exact text was selected or represented?
- What surrounding context was captured?
- What canonical offsets were captured?
- What is the current best known status?
- What is the current best resolved location, if any?
- Who created the anchor and when?
- What feature owns the anchor: comment, resume, progress, or another purpose?

Minimum fields:

| Field | Rule |
|-------|------|
| `Id` | Database identity. |
| `SectionId` | Required. Container only; not anchor identity. |
| `OriginalSectionVersionId` | Required when a version exists; nullable only for legacy pre-version fallback. |
| `Purpose` | Required enum. |
| `CreatedByUserId` | Required for user-created anchors. |
| `CreatedAt` | Required UTC timestamp. |
| `OriginalSnapshot` | Required immutable selector data. |
| `CurrentMatch` | Nullable; present only when the anchor is currently located. |
| `Status` | Required, derived through domain methods only. |
| `UpdatedAt` | Set when derived match or status changes. |

### 3.3 Original Snapshot Required Data

The original snapshot is immutable. It should include:

| Field | Rule |
|-------|------|
| `SelectedText` | Original selected text as displayed to the reader, trimmed only by explicit capture rules. |
| `NormalizedSelectedText` | Canonical normalized selected text. |
| `SelectedTextHash` | Stable hash of normalized selected text. |
| `PrefixContext` | Canonical text immediately before the selection. |
| `SuffixContext` | Canonical text immediately after the selection. |
| `StartOffset` | Zero-based offset in canonical text. |
| `EndOffset` | Exclusive offset in canonical text. |
| `CanonicalContentHash` | Hash of the full canonical text for the original content. |
| `HtmlSelectorHint` | Optional rendered DOM/path hint for UI highlighting; never authoritative. |

Context length should default to 120 canonical characters on each side unless RS-A Phase A1
finds a better project-specific threshold. Context may be shorter at content boundaries.

### 3.4 Current Match Required Data

The current match is derived and replaceable:

| Field | Rule |
|-------|------|
| `TargetSectionVersionId` | Required when a target version exists. |
| `StartOffset` | Zero-based canonical text offset in target content. |
| `EndOffset` | Exclusive canonical text offset in target content. |
| `MatchedText` | Canonical target text for audit/debugging. |
| `ConfidenceScore` | Integer 0-100. |
| `MatchMethod` | Exact, context, fuzzy, AI, or manual relink. |
| `ResolvedAt` | UTC timestamp. |
| `ResolvedByUserId` | Required for manual relink, null for automated methods. |
| `Reason` | Optional short system or user reason. |

### 3.5 Status Model

Use explicit statuses. Do not infer user-facing state from null checks alone.

| Status | Meaning |
|--------|---------|
| `Original` | Anchor is being displayed in its original version. |
| `Exact` | Automated exact text match in target version. |
| `Context` | Automated context-assisted match. |
| `Fuzzy` | Automated approximate match. |
| `AiMatched` | AI-assisted match accepted within thresholds. |
| `UserRelinked` | Human selected the current location. Highest authority. |
| `UserRejected` | Human rejected the proposed current location. |
| `Orphaned` | No trustworthy current location exists. |

Status transitions must be performed through domain methods.

### 3.6 Domain Invariants

The domain must enforce:

- `EndOffset` must be greater than `StartOffset`.
- Selected text must not be empty after canonical normalization.
- Confidence must be between 0 and 100.
- Manual relink requires an actor.
- User rejection requires an actor.
- Original snapshot fields cannot be changed after creation.
- A rejected match cannot be treated as active.
- `UserRelinked` cannot be overwritten by automated relocation for the same target version.
- An anchor cannot reference a soft-deleted section as an active current location.

---

## 4. Canonical Text and Selection Contract

### 4.1 Canonicalization Rules

All matching and offset storage must use canonical text, not raw HTML.

Canonicalization must:

- Decode HTML entities.
- Remove markup while preserving reader-visible text order.
- Collapse runs of whitespace to a single space.
- Normalize non-breaking spaces to regular spaces.
- Trim leading/trailing whitespace for selected text comparison.
- Preserve paragraph boundaries as at least one whitespace separator.
- Ignore purely decorative markup and style attributes.
- Produce deterministic output for the same HTML.

The implementation may add a dedicated canonicalization service. If it does, it belongs
in Application if it orchestrates existing parsers, or Infrastructure if it depends on a
specific HTML parser package. Domain receives canonical strings and offsets; it does not
parse HTML.

### 4.2 Browser Capture Contract

JavaScript capture must produce a DTO with:

- `sectionId`
- `sectionVersionId` when available
- selected/display text
- canonical selected text if available
- canonical start/end offsets if available
- prefix/suffix context if available
- optional DOM selector/path hint

Server-side code must validate and canonicalize again. Browser-provided offsets are hints,
not trusted authority.

### 4.3 Selection Validity

Valid selections:

- Must be inside one reader content surface.
- Must map to non-empty canonical text.
- May span inline markup.
- May span paragraphs if canonical offsets remain valid.

Invalid selections:

- Empty selections.
- Selections outside published reader content.
- Selections spanning multiple sections.
- Selections that cannot be mapped to canonical content.

Invalid selections must fail with validation feedback, not partial persistence.

---

## 5. Relocation Pipeline

The relocation pipeline resolves an original anchor into a target content version.

### 5.1 Pipeline Order

Run methods in this order:

1. Manual relink for the same target version, if present.
2. Exact normalized selected text match.
3. Context-assisted match.
4. Fuzzy match.
5. AI-assisted match, only when enabled and deterministic methods fail or score below threshold.
6. Orphan.

### 5.2 Exact Matching

Exact matching compares normalized selected text against target canonical text.

Rules:

- One match: confidence 100, method `Exact`.
- No matches: pass to context matching.
- Multiple matches: use context disambiguation; if still tied, do not choose silently.

### 5.3 Context Matching

Context matching uses prefix and suffix context around candidate exact or near-exact
selected text.

Rules:

- Prefer candidates with both prefix and suffix context agreement.
- Allow shorter context at content boundaries.
- If selected text appears multiple times and context identifies one candidate, method is
  `Context`.
- If context cannot disambiguate, pass to fuzzy matching or orphan.

Suggested confidence:

| Condition | Confidence |
|-----------|------------|
| Exact text plus both contexts match | 95 |
| Exact text plus one context matches | 85 |
| Exact text but weak context | 70 |

### 5.4 Fuzzy Matching

Fuzzy matching compares original selected text and context against target canonical text
using deterministic string similarity.

Rules:

- Fuzzy matching must be deterministic.
- Confidence must be numeric and explainable.
- Do not match below 70.
- Scores 70-84 must be shown as approximate when surfaced to users.
- Ties within 5 points must not be auto-selected unless context breaks the tie.

Suggested threshold:

| Score | Status |
|-------|--------|
| 90-100 | `Fuzzy`, high confidence |
| 80-89 | `Fuzzy`, medium confidence |
| 70-79 | `Fuzzy`, low confidence |
| 0-69 | Orphan unless AI is enabled |

### 5.5 AI Recovery

AI recovery is optional and must remain last resort.

Rules:

- AI must not run before deterministic matching.
- AI must return candidate location, confidence, and rationale.
- AI confidence must be capped unless deterministic evidence supports it.
- AI matches below the activation threshold become orphaned.
- AI output must never overwrite a manual relink.

Default activation threshold: 85.

### 5.6 Orphan Behavior

When no trustworthy location exists:

- Set status to `Orphaned`.
- Keep original anchor and original context available.
- Keep the owning comment or progress record visible where appropriate.
- Do not invent a current location.

---

## 6. Application Services

The exact names may be adjusted during RS-A Phase A1, but the application surface should
provide these responsibilities.

### 6.1 Anchor Service

Creates and retrieves anchors.

Expected operations:

- Create an anchor from a validated selection.
- Get anchor by id with current status.
- Get anchors for a section/version.
- Get anchors referenced by a comment or by `ReadEvent.ResumeAnchorId`.

### 6.2 Anchor Resolution Service

Runs relocation and returns a current match or orphan state.

Expected operations:

- Resolve one anchor against a target `SectionVersion`.
- Resolve all anchors for a section after a new version is published.
- Return confidence and method.
- Avoid mutating original snapshot data.

### 6.3 Anchored Comment Service

Coordinates comment creation with anchor creation.

Expected operations:

- Create an anchored comment from selected text.
- Keep existing non-anchored comments working.
- Retrieve comments with current anchor status and original context metadata.

### 6.4 Reader Resume Service

Coordinates anchor-based reader resume.

Expected operations:

- Capture latest reader position as an anchor.
- Resolve resume anchor when a reader opens a section.
- Fall back safely when no anchor exists or the anchor is orphaned.
- Preserve existing read-event behavior until RS-B integration activates anchor restore.
- Store the latest active resume anchor on `ReadEvent.ResumeAnchorId`.

### 6.5 Original Context Service

Retrieves original content for an anchor.

Expected operations:

- Load original `SectionVersion.HtmlContent` when present.
- Fall back to legacy content only when no original version exists.
- Return original selected text, context, and version metadata.
- Provide enough data for Web to navigate/highlight original context.

### 6.6 Human Override Service

Coordinates permission checks and domain state transitions.

Expected operations:

- Reject a proposed match.
- Relink an anchor to a user-selected passage.
- Persist actor, timestamp, target version, and optional reason.
- Prevent unauthorized overrides.

---

## 7. Persistence Contract

RS-A Phase A3 must keep persistence additive.

### 7.1 Tables and Relationships

Expected persistence shape:

- `PassageAnchors`
- Optional owned table or JSON/owned columns for original snapshot
- Owned columns or an owned type for the current match
- Nullable FK from `Comment.PassageAnchorId` to `PassageAnchor`
- Nullable FK from `ReadEvent.ResumeAnchorId` to `PassageAnchor`

Use EF owned types for the original snapshot and current match unless a concrete provider
limitation is found during A3. Do not add `PassageAnchorMatches` in RS-A; if audit/history
becomes important, add a separate table in a later phase.

### 7.2 Indexes

Minimum indexes:

- `SectionId`
- `OriginalSectionVersionId`
- `Purpose`
- `CreatedByUserId`
- `Comments.PassageAnchorId`
- `ReadEvents.ResumeAnchorId`

### 7.3 Backward Compatibility

- Existing comments get null anchor references.
- Existing read events get null anchor references.
- Existing views must keep rendering null-anchor records.
- Migration must not require backfilling anchors for existing data.

---

## 8. Web and UX Contract

### 8.1 Existing Behavior Preservation

Until a sprint explicitly replaces a behavior:

- Existing comments continue to render.
- Existing reader progress/read events continue to work.
- Existing version labels and update messaging continue to work.
- Reader pages remain usable with JavaScript disabled, except for new selection capture.

### 8.2 Inline Comments

Inline comment UI must:

- Allow selecting text inside one section.
- Submit through an application service.
- Render anchored comment indicators without corrupting story HTML.
- Keep comment list/sidebar behavior available.
- Show orphaned/approximate state where relevant.

### 8.3 Resume

Resume UI must:

- Capture position without excessive network writes.
- Debounce client updates.
- Restore to resolved anchor when trustworthy.
- Fall back to existing safe behavior when anchor resolution fails.

### 8.4 Original Context

Original context UI must:

- Clearly distinguish original text from current text.
- Show original version metadata when available.
- Navigate/highlight original anchor when possible.
- Never imply that original context is current content.

---

## 9. Security and Permissions

### 9.1 Creation

- Readers may create anchors for their own comments and resume positions where they
  already have access to the content.
- Authors may create anchors where author workflows require them.
- System Support does not gain extra content access through anchors.

### 9.2 Viewing

- A user may view an anchor only if they may view the owning comment, read event, or
  section content.
- Author insight must not expose reader-private detail beyond existing product decisions.

### 9.3 Override

Only these actors may reject or relink:

- The comment owner for that comment's anchor.
- The project author for anchors in their project.

All overrides must persist actor and timestamp.

---

## 10. Sprint and Phase Execution Plan

Each phase prompt must extract scope, deliverables, implementation steps, stop conditions,
and completion criteria from this section.

### RS-A - Anchor Foundation

Goal: introduce anchor model, persistence, and application surface without changing UI
behavior.

#### A1 - Model Discovery

Deployable: yes. No code changes.

Scope:

- Inspect `Comment`, `ReadEvent`, `Section`, `SectionVersion`, reader views, author views,
  repositories, and current services.
- Propose final class names, relationships, repository shape, and migration strategy.
- Identify whether `ReadEvent` is sufficient for anchor-based progress or whether a
  separate progress record is needed later.
- Completed decision: use `PassageAnchor`, `PassageAnchorSnapshot`, and
  `PassageAnchorMatch`; add nullable `Comment.PassageAnchorId` and
  `ReadEvent.ResumeAnchorId`; no separate progress-history table in RS-A.

Deliverable:

- A written proposal only.
- No production code.
- No migrations.

Definition of done:

- Proposal names exact files/classes to change in A2-A4.
- Proposal confirms how legacy comments/read events remain valid.
- Proposal confirms versioning boundary compliance.

Stop if:

- Current model cannot support additive migration.
- Proposed design would require reader content to use `Section.HtmlContent` when a
  `SectionVersion` exists.

#### A2 - Domain Definition (TDD)

Deployable: non-deployable alone.
Reason: domain model has no persistence until A3.
Must deploy with: A3.

Scope:

- Add domain entity/value objects/enums for anchors and matches.
- Use the exact A1-selected files/classes listed in Section 3.1.1.
- Add nullable anchor references to `Comment` and `ReadEvent` at the domain level.
- Add domain methods for creation, match update, orphaning, rejection, and relink.
- Do not add EF mappings or Web usage.

Deliverable:

- Domain tests first.
- Minimal domain implementation.

Required tests:

- Create anchor with valid snapshot succeeds.
- Existing comments can be created with no `PassageAnchorId`.
- Existing read events can be created with no `ResumeAnchorId`.
- Read event resume anchor can be updated and cleared.
- Empty selected text throws.
- End offset before start offset throws.
- Original snapshot cannot be mutated through domain API.
- Confidence outside 0-100 throws.
- Orphan transition clears active current match.
- User rejection requires actor id.
- Manual relink requires actor id and valid target.
- Automated match cannot overwrite manual relink for same target version.

Definition of done:

- Domain tests pass.
- Full suite passes where locally required.
- No persistence or Web changes.

#### A3 - Persistence

Deployable: yes when combined with A2.

Scope:

- Add EF mappings, repository interfaces/implementations, and migration.
- Add nullable FK links from `Comments.PassageAnchorId` and `ReadEvents.ResumeAnchorId`
  to `PassageAnchors`.
- Keep schema additive.

Deliverable:

- Infrastructure persistence tests.
- Migration.
- Repository methods needed by A4.

Required tests:

- Anchor persists and reloads with immutable snapshot.
- Null anchor comments remain valid.
- Null anchor read events remain valid.
- `Comment.PassageAnchorId` persists and reloads when present.
- `ReadEvent.ResumeAnchorId` persists and reloads when present.
- Current match persists and reloads.
- Migration does not require existing comment/read-event rows to be updated.

Definition of done:

- Migration is additive.
- Existing tests pass.
- A2+A3 is production safe even though no UI uses anchors yet.

#### A4 - Application Surface

Deployable: yes.

Scope:

- Add application DTOs and services for anchor create/retrieve.
- Use the exact A1-selected DTO/service files listed in Section 3.1.1.
- Add authorization checks at application boundary.
- Do not change reader or comment UI yet.

Deliverable:

- Application service tests.
- Service interfaces registered in DI if required.

Required tests:

- Create anchor for accessible section/version succeeds.
- Create anchor rejects invalid selection.
- Create anchor rejects unauthorized user.
- Retrieve anchor returns status and original metadata.
- Existing non-anchor comment flows are unaffected.

Definition of done:

- Anchor service exists but is unused by UI.
- No behavior change for current users.

### RS-B - Anchored Resume

Goal: replace fragile scroll-only resume with anchor-based resume.

#### B1 - Capture

Deployable: non-deployable alone.
Reason: capture without restore creates unused state and inconsistent expectations.
Must deploy with: B2 and B3.

Scope:

- Add Web capture endpoint or extend existing read-progress endpoint.
- Capture debounced reading position as an anchor purpose.
- Preserve existing read-event fields.

Deliverable:

- Web/Application tests for capture path.
- JavaScript capture with server validation.

Definition of done:

- Position anchor can be saved.
- Existing resume behavior is still the active behavior until B3.

#### B2 - Restore

Deployable: non-deployable alone.
Reason: restore must be integrated with capture and existing read events.
Must deploy with: B1 and B3.

Scope:

- Resolve resume anchor when reader opens content.
- Return target offsets or safe fallback to Web.
- Do not remove existing scroll fallback yet.

Deliverable:

- Application tests for restored exact/context/orphan behavior.
- Web model fields needed by reader views.

Definition of done:

- Resume target is available to views.
- Orphaned resume anchors fall back safely.

#### B3 - Integration

Deployable: yes.

Scope:

- Make anchor-based resume the primary path.
- Keep scroll fallback for legacy/null-anchor records.
- Ensure reader open/read events still update correctly.

Deliverable:

- End-to-end reader resume behavior.
- Existing reader flows preserved.

Definition of done:

- Reader resumes from anchor when possible.
- Reader falls back safely when not possible.

#### B4 - Tests

Deployable: yes.

Scope:

- Complete cross-version resume coverage.
- Add regression tests for old behavior fallback.

Definition of done:

- Tests cover exact, context, orphan, legacy scroll fallback, and unauthorized access.

### RS-C - Inline Comments

Goal: create and render comments anchored to selected text.

#### C1 - Selection Capture

Deployable: non-deployable alone.
Reason: selection capture without comment creation has no user value.
Must deploy with: C2 and C3.

Scope:

- Capture selected text and context from reader content.
- Validate single-section selection.
- Do not persist comments yet.

Definition of done:

- Capture DTO is validated server-side.
- Invalid selections fail safely.

#### C2 - Comment Creation

Deployable: non-deployable alone.
Reason: persisted inline comments require rendering before safe user release.
Must deploy with: C1 and C3.

Scope:

- Extend comment creation to optionally create an anchor.
- Existing non-inline comments remain supported.
- Comment creation uses application service, not controller logic.

Definition of done:

- Anchored comments persist.
- Null-anchor comments still work.

#### C3 - Rendering

Deployable: yes with C1/C2.

Scope:

- Render inline indicators/highlights for anchored comments.
- Keep comment list/sidebar available.
- Mark approximate/orphaned anchors clearly.

Definition of done:

- Inline comments are visible without corrupting story content.
- Orphaned comments remain visible.

#### C4 - Tests

Deployable: yes.

Scope:

- Complete test coverage for selection, creation, rendering, and legacy comments.

Definition of done:

- Tests cover selected text across inline markup, invalid selections, null anchors,
  orphaned anchors, and authorization.

### RS-D - Deterministic Relocation

Goal: resolve anchors across versions without AI.

#### D1 - Exact Matching

Deployable: non-deployable alone.
Reason: exact matching is only one stage of the relocation pipeline.
Must deploy with: D5.

Scope:

- Implement exact normalized-text matching.
- No UI activation yet.

Definition of done:

- Exact unique matches return confidence 100.
- Duplicate exact matches do not silently choose the wrong location.

#### D2 - Context Matching

Deployable: non-deployable alone.
Reason: context matching must be integrated with the full pipeline.
Must deploy with: D5.

Scope:

- Implement prefix/suffix context disambiguation.

Definition of done:

- Context disambiguates repeated text.
- Weak context returns lower confidence or passes to next stage.

#### D3 - Fuzzy Matching

Deployable: non-deployable alone.
Reason: fuzzy results require confidence scoring and integration.
Must deploy with: D4 and D5.

Scope:

- Implement deterministic fuzzy matching.
- Do not activate AI.

Definition of done:

- Fuzzy scores are deterministic.
- Low scores do not become matches.

#### D4 - Confidence Scoring

Deployable: non-deployable alone.
Reason: confidence values must be consumed by integrated relocation.
Must deploy with: D5.

Scope:

- Centralize confidence scoring and thresholds.
- Map scores to status and user-facing confidence.

Definition of done:

- Confidence is always 0-100.
- Threshold behavior is tested.

#### D5 - Integration

Deployable: yes.

Scope:

- Wire exact/context/fuzzy into application resolution service.
- Resolve anchors for target versions.
- Persist current match or orphan state.

Definition of done:

- Deterministic relocation pipeline is active.
- No AI dependency.
- Orphans degrade safely.

### RS-E - Human Override

Goal: allow users to correct automated relocation.

#### E1 - Permissions

Deployable: yes.

Scope:

- Add application authorization rules for reject/relink.
- No UI required.

Definition of done:

- Only comment owner or project author may override.
- Unauthorized attempts are rejected.

#### E2 - Reject Match

Deployable: non-deployable alone.
Reason: reject action needs integrated status display.
Must deploy with: E4.

Scope:

- Add domain/application reject operation.
- Persist actor, timestamp, target version, and optional reason.

Definition of done:

- Rejected match is no longer active.
- Original context remains available.

#### E3 - Relink

Deployable: non-deployable alone.
Reason: relink needs integrated status display and selection UI.
Must deploy with: E4.

Scope:

- Add domain/application relink operation from a new selected passage.
- Manual relink outranks automated matches.

Definition of done:

- Relink creates authoritative current match.
- Automated relocation cannot overwrite relink for same target version.

#### E4 - Integration

Deployable: yes.

Scope:

- Add UI/API integration for reject and relink.
- Show status and audit metadata where appropriate.

Definition of done:

- Users can reject wrong matches and relink to correct passages.
- Status is visible and not misleading.

### RS-F - Original Context

Goal: allow users to inspect the original passage and version.

#### F1 - Retrieval

Deployable: non-deployable alone.
Reason: retrieval without navigation/UI is not user-facing.
Must deploy with: F2 and F3.

Scope:

- Application service returns original version metadata and original passage context.

Definition of done:

- Original context loads from original `SectionVersion` where available.
- Legacy fallback is explicit and tested.

#### F2 - Navigation

Deployable: non-deployable alone.
Reason: navigation needs UI integration.
Must deploy with: F3.

Scope:

- Provide navigation/highlight data for original passage.

Definition of done:

- Original offsets can be mapped to rendered content.
- Failures degrade to context display.

#### F3 - UI Integration

Deployable: yes.

Scope:

- Add "view original context" behavior for anchored comments.
- Clearly label original vs current.

Definition of done:

- Users can inspect original context without confusing it for current content.

### RS-G - AI-Assisted Relocation

Goal: use AI only as a last-resort relocation assistant.

#### G1 - Integration

Deployable: non-deployable alone.
Reason: AI integration needs confidence handling before activation.
Must deploy with: G2 and G3.

Scope:

- Integrate with `AIScoringService` or its established abstraction.
- AI receives bounded candidate/context data, not entire project content.

Definition of done:

- AI can propose a candidate without activating user-facing behavior.

#### G2 - Confidence Handling

Deployable: non-deployable alone.
Reason: confidence handling must be activated with the AI pipeline.
Must deploy with: G3.

Scope:

- Cap AI confidence.
- Combine AI rationale with deterministic evidence where available.

Definition of done:

- AI matches below threshold become orphaned.
- AI cannot claim exact confidence.

#### G3 - Activation

Deployable: yes.

Scope:

- Enable AI recovery after deterministic pipeline fails.
- Feature flag or configuration gate if needed.

Definition of done:

- AI is last resort.
- User relink and rejection remain authoritative.
- Failures degrade to orphan.

### RS-H - Reader Insight

Goal: expose anchor-based progress and activity insight.

#### H1 - Progress Tracking

Deployable: non-deployable alone.
Reason: tracking data needs author insight/UI to be useful.
Must deploy with: H2 and H3.

Scope:

- Persist anchor-based progress in or alongside `ReadEvent`.
- Do not expose new author UI yet.

Definition of done:

- Progress anchor records latest known reader position.
- Existing read event behavior still works.

#### H2 - Author Insight

Deployable: non-deployable alone.
Reason: insight data needs UI integration.
Must deploy with: H3.

Scope:

- Application service returns reader activity and progress summaries.
- Respect existing privacy/product decisions.

Definition of done:

- Author can query progress summaries through Application layer.
- Web does not calculate progress.

#### H3 - UI Integration

Deployable: yes.

Scope:

- Add author-facing progress indicators and drill-downs.
- Keep UI clear and non-invasive.

Definition of done:

- Author sees reader progress using application-provided data.
- Reader privacy and authorization tests pass.

---

## 11. Test Strategy

### 11.1 Domain Tests

Domain tests must cover:

- Entity/value object creation invariants.
- Status transitions.
- Confidence validation.
- Original snapshot immutability.
- Manual relink and rejection authority at domain method level.

### 11.2 Application Tests

Application tests must cover:

- Authorization.
- Service orchestration.
- Repository interaction boundaries.
- Null-anchor legacy behavior.
- Versioning boundary behavior.
- Safe fallback behavior.

### 11.3 Infrastructure Tests

Infrastructure tests must cover:

- EF mappings.
- Migration compatibility.
- Repository queries.
- Owned type or related-table persistence.

### 11.4 Web Tests

Web tests must cover:

- Thin controller behavior.
- ViewModel mapping.
- Validation failure paths.
- Rendering states: exact, approximate, orphaned, rejected, relinked.
- No repository access from controllers.

---

## 12. Stop Conditions

Stop and request clarification if:

- A phase requires changing reader content resolution away from `SectionVersion`.
- A migration would require destructive schema change or mandatory data backfill.
- A Web controller needs to own business logic to complete the phase.
- Anchor ownership between comments/read events cannot be represented additively.
- Matching thresholds would cause ambiguous matches to appear certain.
- AI would need to run before deterministic relocation.
- User privacy rules for author insight are unclear.

---

## 13. Completion Criteria

The RSprint series is complete only when:

- Readers can resume across versions using anchors.
- Readers can comment on selected text.
- Authors can see comments and progress accurately.
- Anchors relocate safely across published versions.
- Original context remains available.
- Users can reject and relink incorrect matches.
- AI, if enabled, is last resort and confidence-bounded.
- Existing comments and read events remain valid.
- Full test suite passes.

---

## 14. First Execution Step

Start with RS-A Phase A1 - Model Discovery.

A1 is complete. The selected model contract is recorded in Section 3.1.1. Continue with
RS-A Phase A2 using that contract. A2 must not revisit the model discovery decision unless
it hits a documented stop condition.

---

# Explanations in Basic Terms - Non-authoritative Summary

## RS-A — Anchor Foundation

What it means:
You teach the system to understand:

> “this exact piece of text”

Instead of:

> “somewhere in Scene 3”

In plain terms:

Identify a chunk of text
Store enough information to find it again later
Do this without breaking anything existing

**Outcome:**
The system can point to a specific passage, not just a location.

## RS-B — Anchored Resume

What it means:
When a reader leaves and comes back:

> they return to the same place in the text, even if things changed

In plain terms:

- Remember where the reader was reading
- Use the anchor to find that place again
- If the text moved, find the closest match

**Outcome:**
“Continue reading” actually works reliably.

## RS-C — Inline Comments

What it means:
Readers can highlight text and comment on it directly.

In plain terms:

Select a sentence or paragraph
Attach a comment to that exact text
Show that comment in context

**Outcome:**
Comments become precise, not general.

## RS-D — Deterministic Relocation

What it means:
When the author edits the text:

comments don’t break — they move with the text

In plain terms:

Try to find the same text in the new version
If not exact, find something very close
Do this using rules, not AI

**Outcome:**
Comments survive edits like:

## RS-E — Human Override

What it means:
When the system matches a comment:

this is the wrong place

Instead of:

the correct passage

In plain terms:

Let the reader or author say “this is the wrong place”
Let them relink the comment to the correct passage
Record who made the change

**Outcome:**
The system can be corrected when it gets things wrong.

## RS-F — Original Context

What it means:
When a version changes:

the original passage is still available

In plain terms:

Store the original version of the text
Allow viewing the comment in that original passage
Do not lose historical context

**Outcome:**
The system always shows the true original meaning.

## RS-G — AI-Assisted Relocation

What it means:
When the system cannot find a match:

use AI to locate the passage

Instead of:

failing to match at all

In plain terms:

Try rule-based matching first
If that fails, use AI to find a similar passage
Only apply if confidence is high

**Outcome:**
Comments can still be recovered after major rewrites.

## RS-H — Reader Insight

What it means:
Once this data exists:

understand how readers engage with the text

In plain terms:

Track how far readers get
Show authors reader progress
Surface useful engagement insights

**Outcome:**
Authors gain visibility into how their work is read.