Below is **F1 rewritten as a Copilot-ready execution prompt**.

````markdown
# RS-F Phase F1 - Original Context Retrieval

## Execution Mode

Cloud Execution Phase.

Apply the **Test Execution Override - Cloud Phases** rules from `AGENTS.md`.

This is a hard-gated, accuracy-first phase. Do not infer file locations, contracts, DTO shapes, or service names from convention. Read the required documents and current source files before changing code.

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

- `PassageAnchor`
- `PassageAnchorSnapshot`
- `PassageAnchorMatch`
- `Section`
- `SectionVersion`
- comment ownership and comment visibility rules
- reader access rules
- existing application service patterns
- existing repository interfaces used by Application services
- current test project structure

Do not proceed until the current file content has been inspected.

---

## Objective

Complete **RS-F Phase F1 - Retrieve Original Version Content**.

Sprint goal: allow users to inspect the original passage and version for an anchored comment or anchored reader-facing record.

This phase retrieves original context only. It must not add UI, navigation, highlighting, JavaScript, Razor changes, or controller actions unless a current source document already requires a minimal application boundary test host.

---

## Deployability

**Deployable:** No.

**Reason:** Retrieval without navigation and UI is not user-facing.

**Must be deployed with:** F2 and F3.

F1 must be safe to merge into `RS-F-base`, but must not be released to production alone.

---

## Branching

1. Checkout `main`.
2. Pull latest from `origin/main`.
3. Create `RS-F-base` from `main` if it does not already exist.
4. Create `RS-F-base/phase-f1-retrieval` from `RS-F-base`.
5. Commit all work for this phase on `RS-F-base/phase-f1-retrieval`.
6. Developer merge path is:
   - `RS-F-base/phase-f1-retrieval` -> `RS-F-base`
   - later `RS-F-base` -> `main` only after F1, F2, and F3 are complete.

---

## Source of Truth

Primary source:

- `Passage Anchoring, Reader Continuity, and Inline Commentary.md`
- Section 6.5, Original Context Service
- Section 8.4, Original Context
- Section 10, RS-F / F1 - Retrieval

Task index source:

- `TASKS.md`
- RSprint section for `RS-F - Original Context`

If this prompt conflicts with the source document, stop immediately and report the conflict before changing code.

---

## Required Behaviour

Implement application-layer retrieval of original anchor context.

The retrieval operation must be driven by a `PassageAnchorId`.

The service must return original context from the anchor’s immutable original snapshot and from the original content source.

When `PassageAnchor.OriginalSectionVersionId` is present:

- load the matching `SectionVersion`
- use `SectionVersion.HtmlContent` as the original content source
- do not use `Section.HtmlContent`

When `PassageAnchor.OriginalSectionVersionId` is null:

- use the explicit legacy fallback path
- fallback must be visible in the returned DTO
- fallback must be covered by tests
- fallback must not run when an original section version exists

Missing original content must fail safely through an explicit result state. Do not throw unhandled exceptions for ordinary missing data.

---

## Required Application Contract

Add or extend an Application service for original context retrieval.

Preferred service name unless current code already establishes a different compatible name:

- `IOriginalContextService`
- `OriginalContextService`

Preferred location unless current project structure requires an existing equivalent location:

- Interface in Application abstraction/contracts area if that is the current project pattern
- Implementation in Application services area

Do not place this service in Web.

Do not call EF `DbContext` from Web.

Do not call repositories from Web.

Do not add business logic to controllers.

---

## Required Method Contract

Add one retrieval method with this responsibility:

```csharp
Task<OriginalContextResultDto> GetOriginalContextAsync(
    Guid passageAnchorId,
    Guid requestingUserId,
    CancellationToken cancellationToken = default);
````

If current application service conventions require a different result wrapper, use the existing project convention, but preserve these required semantics exactly.

The method must:

1. Load the `PassageAnchor`.
2. Authorize the requesting user through Application-layer rules.
3. Load original version content when `OriginalSectionVersionId` exists.
4. Use legacy fallback only when `OriginalSectionVersionId` is null.
5. Return original selected text, original context, offsets, content-source metadata, and a clear success/failure state.
6. Avoid mutating `PassageAnchor`, `PassageAnchorSnapshot`, `PassageAnchorMatch`, comments, read events, sections, or section versions.

---

## Required DTO Contract

Create DTOs only if no suitable existing DTOs already exist.

The retrieval result must include these fields or clear equivalents:

```csharp
public sealed class OriginalContextResultDto
{
    public bool Succeeded { get; init; }
    public OriginalContextFailureReason? FailureReason { get; init; }
    public OriginalContextDto? Context { get; init; }
}
```

```csharp
public enum OriginalContextFailureReason
{
    NotFound,
    Unauthorized,
    OriginalContentMissing
}
```

```csharp
public sealed class OriginalContextDto
{
    public Guid PassageAnchorId { get; init; }
    public Guid SectionId { get; init; }

    public Guid? OriginalSectionVersionId { get; init; }
    public bool IsLegacyFallback { get; init; }

    public string OriginalSelectedText { get; init; } = string.Empty;
    public string NormalizedSelectedText { get; init; } = string.Empty;
    public string PrefixContext { get; init; } = string.Empty;
    public string SuffixContext { get; init; } = string.Empty;

    public int StartOffset { get; init; }
    public int EndOffset { get; init; }

    public string OriginalHtmlContent { get; init; } = string.Empty;

    public string? OriginalVersionLabel { get; init; }
    public int? OriginalVersionNumber { get; init; }
    public DateTime? OriginalVersionCreatedAtUtc { get; init; }
}
```

Rules:

* `OriginalHtmlContent` is the original source content for F2/F3 processing.
* `StartOffset` and `EndOffset` are canonical offsets from the immutable original snapshot.
* `OriginalSelectedText`, `NormalizedSelectedText`, `PrefixContext`, and `SuffixContext` come from the immutable original snapshot.
* `IsLegacyFallback` must be `false` when `OriginalSectionVersionId` exists.
* `IsLegacyFallback` must be `true` only when no original version exists and legacy content is deliberately used.
* The DTO must not claim that original content is current content.
* Do not include current match data unless an existing contract requires it. F1 is about original context only.

If existing entity or DTO property names differ, map to the existing names, but preserve the semantics above.

---

## Authorization Rules

The requesting user may retrieve original context only if they are allowed to view the owning record or the reader-facing section content.

At minimum, tests must cover:

* authorized owner or permitted reader succeeds
* project author succeeds when existing product rules permit author access to that anchor/comment context
* unauthorized user fails

Do not grant System Support extra content access through this service.

If ownership cannot be determined from current model relationships, stop and report the exact missing relationship.

---

## Architecture Constraints

* Domain owns invariants and immutable anchor state.
* Application owns original context retrieval and authorization.
* Infrastructure owns persistence only.
* Web must not be changed in F1 unless required only for dependency registration.
* Sync/import code must not create, retrieve, mutate, relocate, or delete anchors.
* `SectionVersion.HtmlContent` is authoritative when `OriginalSectionVersionId` exists.
* `Section.HtmlContent` may only be used for explicit legacy fallback when no original version exists.
* Existing comments without anchors remain valid.
* Existing read events without resume anchors remain valid.
* No schema migration is expected for F1 unless current code proves a required field is missing. If a migration appears necessary, stop and report before changing schema.

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

### Versioned original context

Given an anchor with `OriginalSectionVersionId`
When original context is retrieved
Then the service loads original content from `SectionVersion.HtmlContent`
And does not use `Section.HtmlContent`
And returns `IsLegacyFallback == false`.

### Legacy fallback

Given an anchor with no `OriginalSectionVersionId`
When original context is retrieved
Then the service uses the explicit legacy fallback source
And returns `IsLegacyFallback == true`.

### Snapshot data

Given a valid anchor
When original context is retrieved
Then selected text, normalized selected text, prefix context, suffix context, start offset, and end offset are returned from the immutable original snapshot.

### Version metadata

Given an anchor with an original section version
When original context is retrieved
Then available original version metadata is returned.

### Unauthorized access

Given a user who cannot view the owning content or owning record
When original context is retrieved
Then the service returns a failed result with `FailureReason == Unauthorized`.

### Missing anchor

Given a missing `PassageAnchorId`
When original context is retrieved
Then the service returns a failed result with `FailureReason == NotFound`.

### Missing original content

Given an anchor whose original content source cannot be loaded
When original context is retrieved
Then the service returns a failed result with `FailureReason == OriginalContentMissing`.

### No mutation

Given an anchor
When original context is retrieved
Then the anchor, snapshot, current match, comment, read event, section, and section version are not mutated.

---

## Required Implementation Steps

1. Inspect current Application service, repository, DTO, and test patterns.
2. Identify the existing repositories needed to load:

   * `PassageAnchor`
   * `SectionVersion`
   * fallback `Section` only for legacy anchors
   * ownership/access data
3. Add service stub and DTOs.
4. Add failing Application tests.
5. Implement retrieval using existing repositories.
6. Add DI registration only if required by current project conventions.
7. Run phase-specific tests.
8. Run broader tests required by `AGENTS.md`.
9. Run `git diff --check`.
10. Commit only related changes.

---

## Stop Conditions

Stop immediately and report before changing code if any of the following occur:

* Retrieval would use `Section.HtmlContent` while `OriginalSectionVersionId` exists.
* Original context cannot be authorized through Application-layer rules.
* Ownership of an anchor cannot be determined from current model relationships.
* A migration appears necessary.
* A Web controller would need to own retrieval business logic.
* A repository call from Web appears necessary.
* Required DTO semantics cannot be represented without guessing.
* Current source files conflict with the master RS-F source document.
* Existing comments or read events would need mandatory anchor data.
* Any destructive schema change or mandatory backfill appears necessary.

---

## Out of Scope

Do not implement:

* UI
* Razor changes
* JavaScript
* original context modal
* highlighting
* navigation mapping
* current-content comparison
* relocation
* AI matching
* human relink
* rejection
* author insight
* sync/import changes
* schema changes unless explicitly stopped and approved

---

## Definition of Done

F1 is complete only when:

* Original context retrieval exists in Application.
* Retrieval is driven by `PassageAnchorId`.
* Versioned anchors load original content from `SectionVersion.HtmlContent`.
* Legacy fallback is explicit and tested.
* Unauthorized access is rejected.
* Missing anchor and missing original content fail through explicit result states.
* Snapshot data is returned from the immutable original snapshot.
* Original version metadata is returned where available.
* No Web business logic has been added.
* No UI has been added.
* No unrelated files are changed.
* Phase tests pass.
* Required broader tests pass, or the exact reason they could not be run is documented.
* `git diff --check` passes.
* Changes are committed on `RS-F-base/phase-f1-retrieval`.

---

## Final Instruction

Be precise, conservative, and architecture-led.

Do not infer contracts.

Do not optimise for speed.

This phase succeeds only when original anchor context can be retrieved safely through the Application layer without changing user-facing behaviour.