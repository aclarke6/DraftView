````markdown
# RS-F Phase F2 - Original Context Navigation

## Execution Mode

Local Execution Phase.

Apply the **Test Execution Override - Local Phases** rules from `AGENTS.md`.

This is a hard-gated, accuracy-first phase. Do not infer file locations, DTO shapes, service names, mapping strategy, or UI behaviour from convention. Read the required documents and current source files before changing code.

---

## Required Reading Order

Read these files before making any code changes:

1. `AGENTS.md`
2. `.github/Instructions/refactoring.instructions.md`
3. `.github/Instructions/versioning.instructions.md`
4. `TASKS.md`
5. `Passage Anchoring, Reader Continuity, and Inline Commentary.md`
6. `PRINCIPLES.md`
7. `REFACTORING.md`

Then inspect the current implementation files that define:

- `IOriginalContextService`
- `OriginalContextService`
- `OriginalContextResultDto`
- `OriginalContextDto`
- `PassageAnchor`
- `PassageAnchorSnapshot`
- canonical text handling, if it already exists
- HTML parsing or sanitisation utilities, if they already exist
- current Application service test patterns

Do not proceed until the current file content has been inspected.

---

## Objective

Complete **RS-F Phase F2 - Navigate to Original Anchor**.

Sprint goal: provide safe navigation and highlight data for original context retrieved in F1.

This phase prepares navigation data only. It must not add final UI, Razor rendering, JavaScript interaction, modals, buttons, or controller endpoints unless current architecture requires a minimal Application-facing test seam.

---

## Deployability

**Deployable:** No.

**Reason:** Navigation data without UI integration is not user-facing.

**Must be deployed with:** F3.

F2 must be safe to merge into `RS-F-base`, but must not be released to production alone.

---

## Branching

1. Checkout `RS-F-base`.
2. Pull latest applicable remote state.
3. Create `RS-F-base/phase-f2-navigation` from `RS-F-base`.
4. Commit all work for this phase on `RS-F-base/phase-f2-navigation`.
5. Developer merge path is:
   - `RS-F-base/phase-f2-navigation` -> `RS-F-base`
   - later `RS-F-base` -> `main` only after F1, F2, and F3 are complete.

---

## Source of Truth

Primary source:

- `Passage Anchoring, Reader Continuity, and Inline Commentary.md`
- Section 4, Canonical Text and Selection Contract
- Section 6.5, Original Context Service
- Section 8.4, Original Context
- Section 10, RS-F / F2 - Navigation

Task index source:

- `TASKS.md`
- RSprint section for `RS-F - Original Context`

If this prompt conflicts with the source document, stop immediately and report the conflict before changing code.

---

## Required Behaviour

Extend original context retrieval so the Application layer can return navigation and highlight data for the original passage.

Navigation must be based on the immutable original snapshot offsets:

- `StartOffset`
- `EndOffset`
- `NormalizedSelectedText`
- `PrefixContext`
- `SuffixContext`

The source content for mapping is the original content returned by F1:

- `SectionVersion.HtmlContent` when `OriginalSectionVersionId` exists
- explicit legacy fallback content only when no original version exists

Navigation must not use current version content.

Navigation must not mutate stored HTML.

Navigation must not alter original anchor snapshots.

Navigation must not create, relocate, reject, or relink anchors.

When mapping succeeds, return renderable navigation hints.

When mapping fails, return a context-only fallback.

Failure to map offsets is not a failure to retrieve original context.

---

## Required Mapping Contract

The mapping process must be deterministic.

The mapping process must use the same canonicalisation rules as anchor creation and matching.

At minimum, mapping must:

1. Take original HTML content from `OriginalContextDto.OriginalHtmlContent`.
2. Produce canonical text from that HTML.
3. Validate that `StartOffset` and `EndOffset` are within the canonical text bounds.
4. Validate that the canonical text slice at those offsets matches the original snapshot sufficiently for exact original navigation.
5. Return mapped navigation data only when the offset range is valid and matches the immutable snapshot.
6. Return fallback context data when offsets cannot be mapped safely.

Do not silently guess a nearby location.

Do not perform fuzzy matching in F2.

Do not call the deterministic relocation pipeline.

Do not use AI.

Do not update `PassageAnchor.CurrentMatch`.

---

## Required DTO Contract

Extend or add DTOs only if no suitable existing DTOs already exist.

The F1 result contract must remain valid.

Add navigation data as a child object on `OriginalContextDto`, or an equivalent existing result shape.

Required navigation DTO semantics:

```csharp
public sealed class OriginalContextNavigationDto
{
    public bool CanNavigate { get; init; }
    public OriginalContextNavigationFailureReason? FailureReason { get; init; }

    public int? CanonicalStartOffset { get; init; }
    public int? CanonicalEndOffset { get; init; }

    public string? HighlightText { get; init; }

    public bool IsOriginalContentNavigation { get; init; }
    public bool IsCurrentContentNavigation { get; init; }

    public OriginalContextFallbackDto? Fallback { get; init; }
}
````

```csharp
public enum OriginalContextNavigationFailureReason
{
    OffsetOutOfRange,
    SnapshotTextMismatch,
    HtmlMappingUnavailable
}
```

```csharp
public sealed class OriginalContextFallbackDto
{
    public string OriginalSelectedText { get; init; } = string.Empty;
    public string PrefixContext { get; init; } = string.Empty;
    public string SuffixContext { get; init; } = string.Empty;
}
```

Required rules:

* `IsOriginalContentNavigation` must always be `true`.
* `IsCurrentContentNavigation` must always be `false`.
* `CanNavigate == true` only when the original offsets can be safely mapped.
* `CanNavigate == false` must include a non-null `FailureReason`.
* `CanNavigate == false` must include fallback data.
* `HighlightText` must come from the original canonical content slice, not current content.
* `CanonicalStartOffset` and `CanonicalEndOffset` must be null when `CanNavigate == false`.
* The DTO must never imply that original context is current content.

If current project conventions use result wrappers, preserve the required semantics within the existing convention.

---

## Required Service Contract

Preferred approach:

Extend `IOriginalContextService.GetOriginalContextAsync(...)` so the returned `OriginalContextDto` includes navigation data.

Alternative allowed only if current architecture strongly favours separation:

Add a dedicated Application service:

```csharp
public interface IOriginalContextNavigationService
{
    Task<OriginalContextNavigationDto> GetNavigationAsync(
        Guid passageAnchorId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default);
}
```

If a separate service is used, it must call or reuse the F1 retrieval path rather than duplicate authorization and content loading logic.

Do not duplicate authorization logic.

Do not duplicate original content loading logic.

Do not bypass F1 retrieval rules.

---

## Authorization Rules

F2 must preserve F1 authorization behaviour.

If the user cannot retrieve original context through F1, the user must not receive navigation data.

Tests must prove unauthorized access still fails.

Do not grant System Support extra content access through navigation.

---

## Architecture Constraints

* Domain owns invariants and immutable anchor state.
* Application owns navigation mapping orchestration.
* Infrastructure owns persistence only.
* Web must not be changed in F2 unless dependency registration is required.
* Do not add Razor changes.
* Do not add JavaScript.
* Do not add final UI.
* Do not call repositories or DbContext from Web.
* Do not mutate HTML.
* Do not store navigation data in the database.
* Do not add migrations.
* Do not use current version content for original navigation.
* Existing comments without anchors remain valid.
* Existing read events without resume anchors remain valid.

---

## TDD Requirements

Use TDD for Application changes.

1. Create a production stub with `NotImplementedException` where a new service or method is required.
2. Write failing tests proving the required behaviour.
3. Confirm tests fail for the expected reason.
4. Implement the smallest production change required to pass.
5. Refactor only after tests are green.
6. Keep refactors local to this phase.

---

## Required Tests

Add or update Application tests for these behaviours:

### Mapped original offsets

Given original HTML content and an anchor snapshot with valid canonical offsets
When original context is retrieved
Then navigation returns `CanNavigate == true`
And returns the original canonical start and end offsets
And returns highlight text from the original content
And marks the navigation as original content navigation only.

### Out-of-range offsets

Given an anchor snapshot whose offsets are outside the original canonical content bounds
When original context is retrieved
Then navigation returns `CanNavigate == false`
And `FailureReason == OffsetOutOfRange`
And fallback context is returned.

### Snapshot text mismatch

Given original HTML content where the canonical slice at the stored offsets does not match the snapshot selected text
When original context is retrieved
Then navigation returns `CanNavigate == false`
And `FailureReason == SnapshotTextMismatch`
And fallback context is returned.

### HTML mapping unavailable

Given original HTML content that cannot be mapped safely by the current implementation
When original context is retrieved
Then navigation returns `CanNavigate == false`
And `FailureReason == HtmlMappingUnavailable`
And fallback context is returned.

### Original not current

Given successful navigation
When navigation data is returned
Then `IsOriginalContentNavigation == true`
And `IsCurrentContentNavigation == false`.

### Unauthorized access

Given a user who cannot retrieve original context
When navigation is requested
Then no navigation data is returned and the result preserves the unauthorized failure from F1.

### No mutation

Given an anchor and original content
When navigation data is produced
Then the anchor, snapshot, current match, comment, read event, section, section version, and HTML content are not mutated.

---

## Required Implementation Steps

1. Inspect F1 implementation and tests.
2. Inspect existing canonicalisation support.
3. If canonicalisation support exists, reuse it.
4. If canonicalisation support does not exist, add the smallest Application-level helper needed for deterministic HTML-to-canonical-text conversion, unless current architecture requires Infrastructure for an existing parser dependency.
5. Add DTO stubs and service stubs as required.
6. Add failing tests.
7. Implement offset validation and exact snapshot-slice validation.
8. Add fallback construction.
9. Add DI registration only if a new service is introduced.
10. Run phase-specific tests.
11. Run broader tests required by `AGENTS.md`.
12. Run `git diff --check`.
13. Commit only related changes.

---

## Stop Conditions

Stop immediately and report before changing code if any of the following occur:

* F1 is not implemented or not available on `RS-F-base`.
* Mapping would require mutating stored HTML.
* Mapping would require current version content.
* Mapping would require fuzzy matching.
* Mapping would require AI.
* Mapping would require storing navigation data in the database.
* A migration appears necessary.
* Authorization would need to be duplicated or weakened.
* A Web controller would need to own navigation logic.
* Required DTO semantics cannot be represented without guessing.
* Existing canonicalisation rules cannot be found and adding a helper would conflict with current architecture.
* Current source files conflict with the master RS-F source document.

---

## Out of Scope

Do not implement:

* UI
* Razor changes
* JavaScript
* modal behaviour
* “View original context” button
* controller endpoints
* current-content comparison
* relocation
* fuzzy matching
* AI matching
* human relink
* rejection
* author insight
* sync/import changes
* schema changes

---

## Definition of Done

F2 is complete only when:

* Original context retrieval returns navigation data or context-only fallback.
* Successful navigation is based only on original content.
* Successful navigation is based on immutable original snapshot offsets.
* Offset out-of-range failure is explicit and tested.
* Snapshot mismatch failure is explicit and tested.
* Mapping unavailable fallback is explicit and tested.
* Navigation data clearly states original content, not current content.
* Unauthorized access remains rejected.
* No Web business logic has been added.
* No UI has been added.
* No schema migration has been added.
* No unrelated files are changed.
* Phase tests pass.
* Required broader tests pass, or the exact reason they could not be run is documented.
* `git diff --check` passes.
* Changes are committed on `RS-F-base/phase-f2-navigation`.

---

## Final Instruction

Be precise, conservative, and architecture-led.

Do not infer contracts.

Do not optimise for speed.

This phase succeeds only when original context navigation data can be produced safely, deterministically, and without implying that original content is current content.

```
```
