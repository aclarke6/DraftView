````markdown
# RS-F Phase F3 - Original Context UI Integration

## Execution Mode

Local Execution Phase.

Apply the **Test Execution Override - Local Phases** rules from `AGENTS.md`.

This is a hard-gated, accuracy-first phase. Do not infer file locations, action names, ViewModel shapes, route names, Razor locations, CSS classes, or UI behaviour from convention. Read the required documents and current source files before changing code.

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
- `OriginalContextNavigationDto`
- anchored comment display
- comment list/sidebar rendering
- reader section/chapter views
- existing comment controller actions
- existing reader controller actions
- existing author comment actions, if authors can inspect anchored comment context
- existing ViewModel mapping patterns
- existing CSS files used by reader/comment views
- existing Web tests for controllers and Razor/ViewModels

Do not proceed until the current file content has been inspected.

---

## Objective

Complete **RS-F Phase F3 - UI Integration**.

Sprint goal: allow users to inspect the original passage and version for an anchored comment or anchored reader-facing record.

This phase adds user-facing behaviour for viewing original context. It must consume F1 retrieval and F2 navigation data. It must not duplicate retrieval, authorization, or mapping logic in Web.

---

## Deployability

**Deployable:** Yes.

F3 is deployable only when F1 and F2 are already merged into `RS-F-base`.

F1, F2, and F3 deploy together as the complete RS-F Original Context capability.

Do not deploy F3 alone if F1 or F2 is missing.

---

## Branching

1. Checkout `RS-F-base`.
2. Confirm F1 and F2 are present on `RS-F-base`.
3. Pull latest applicable remote state.
4. Create `RS-F-base/phase-f3-ui-integration` from `RS-F-base`.
5. Commit all work for this phase on `RS-F-base/phase-f3-ui-integration`.
6. Developer merge path is:
   - `RS-F-base/phase-f3-ui-integration` -> `RS-F-base`
   - `RS-F-base` -> `main` only after F1, F2, and F3 are complete and tested.

---

## Source of Truth

Primary source:

- `Passage Anchoring, Reader Continuity, and Inline Commentary.md`
- Section 6.5, Original Context Service
- Section 8.4, Original Context
- Section 10, RS-F / F3 - UI Integration

Task index source:

- `TASKS.md`
- RSprint section for `RS-F - Original Context`

If this prompt conflicts with the source document, stop immediately and report the conflict before changing code.

---

## Required Behaviour

Add Web integration for viewing original context.

The user-facing behaviour must be driven by `PassageAnchorId`.

The Web layer must:

1. Validate input.
2. Resolve the requesting user identity.
3. Call the Application original context service.
4. Map the Application DTO into a ViewModel.
5. Render a clearly labelled original-context surface.

The Web layer must not:

- load anchors directly
- load section versions directly
- call repositories
- call DbContext
- calculate authorization
- calculate canonical offsets
- map canonical offsets into navigation data
- decide fallback behaviour
- mutate anchor state

---

## Required UI Behaviour

Add a “View original context” behaviour for anchored comments where the current UI has enough anchor information to offer it.

The UI must not offer this behaviour for comments with no `PassageAnchorId`.

The rendered original context surface must clearly state that the content is original historical context, not current manuscript content.

The surface must show:

- original selected text
- prefix context
- suffix context
- original version metadata where available
- whether legacy fallback was used
- navigation or fallback state from F2

When F2 navigation succeeds:

- render the original passage context with the selected passage distinguishable from surrounding context
- do not imply the passage is current content

When F2 navigation fails:

- render the context-only fallback
- show a neutral message that the original passage cannot be safely highlighted
- do not hide the original selected text

Do not redesign the comment system.

Do not add a new JavaScript framework.

Do not use inline styles.

Use existing CSS classes where suitable. Add CSS only to existing project stylesheet locations if current file content shows where the new classes belong.

If CSS changes are made, follow the project CSS version bump rule.

---

## Required ViewModel Contract

Create ViewModels only if no suitable existing ViewModels already exist.

The rendered ViewModel must preserve these semantics:

```csharp
public sealed class OriginalContextViewModel
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

    public string? OriginalVersionLabel { get; init; }
    public int? OriginalVersionNumber { get; init; }
    public DateTime? OriginalVersionCreatedAtUtc { get; init; }

    public bool CanNavigate { get; init; }
    public string? NavigationFailureReason { get; init; }
    public string HighlightText { get; init; } = string.Empty;

    public bool IsOriginalContent { get; init; }
    public bool IsCurrentContent { get; init; }
}
````

Required ViewModel rules:

* `IsOriginalContent` must always be `true`.
* `IsCurrentContent` must always be `false`.
* `CanNavigate` must reflect F2 navigation result.
* `NavigationFailureReason` must be populated when navigation fails.
* `HighlightText` must come from F2 navigation data when available.
* Original selected text must still be populated when navigation fails.
* Do not expose raw original HTML directly to Razor unless current project patterns safely render trusted manuscript HTML.
* Do not render unencoded user-created comment text.
* Do not rename existing ViewModel properties unless required and tested.

If existing project conventions require a different ViewModel shape, preserve the above semantics exactly.

---

## Required Action / Endpoint Contract

Add a Web action or endpoint only in the controller that currently owns comment or reader-context behaviour.

Do not create a new controller unless the current structure clearly requires one.

Preferred route semantics:

* input: `passageAnchorId`
* output: view, partial view, or JSON according to existing comment UI patterns

Required action behaviour:

1. Reject empty `passageAnchorId`.
2. Resolve current user id using existing base/controller helper patterns.
3. Call Application service.
4. Return `NotFound` for `FailureReason == NotFound`.
5. Return `Forbid` or equivalent project convention for `FailureReason == Unauthorized`.
6. Return safe fallback/error view for `FailureReason == OriginalContentMissing`.
7. Return original context view or partial when successful.

Do not add business logic to the controller.

---

## Required Tests

Add or update Web tests for these behaviours:

### Anchored comment exposes original context action

Given a rendered anchored comment with `PassageAnchorId`
When the comment is displayed
Then the UI provides a “View original context” behaviour.

### Non-anchored comment does not expose original context action

Given a rendered comment with no `PassageAnchorId`
When the comment is displayed
Then no “View original context” behaviour is rendered.

### Successful original context rendering

Given the Application service returns original context with navigation success
When the Web action renders the result
Then the response clearly labels the content as original context
And does not label it as current content
And includes original selected text
And includes original version metadata where available.

### Navigation fallback rendering

Given the Application service returns original context with `CanNavigate == false`
When the Web action renders the result
Then the response shows fallback context
And states that the original passage cannot be safely highlighted
And still shows the original selected text.

### Legacy fallback rendering

Given the Application service returns `IsLegacyFallback == true`
When the Web action renders the result
Then the response clearly identifies that legacy fallback content was used.

### Unauthorized access

Given the Application service returns unauthorized
When the Web action is called
Then the Web response is forbidden according to existing project conventions.

### Missing anchor

Given the Application service returns not found
When the Web action is called
Then the Web response is not found according to existing project conventions.

### Missing original content

Given the Application service returns original content missing
When the Web action is called
Then the Web response does not crash
And returns the project’s safe error or fallback response.

### No repository access from Web

Assert or inspect according to existing test conventions that the Web controller does not depend on anchor repositories, section repositories, section-version repositories, or DbContext for this feature.

---

## Required Implementation Steps

1. Inspect F1 and F2 implementation and tests.
2. Inspect current anchored comment rendering.
3. Inspect current controller ownership for reader/comment actions.
4. Inspect existing ViewModel mapping patterns.
5. Inspect existing CSS and reader/comment UI patterns.
6. Add or extend ViewModel.
7. Add failing Web tests.
8. Add controller action or endpoint using Application service only.
9. Add view or partial view using existing UI patterns.
10. Add “View original context” behaviour only for anchored comments.
11. Add CSS only if existing classes are insufficient.
12. If CSS is changed, bump the project CSS version according to project rules.
13. Run phase-specific Web tests.
14. Run broader tests required by `AGENTS.md`.
15. Run `git diff --check`.
16. Commit only related changes.

---

## Stop Conditions

Stop immediately and report before changing code if any of the following occur:

* F1 or F2 is missing from `RS-F-base`.
* UI copy or layout would make original content look current.
* The current comment UI does not expose `PassageAnchorId` and adding it would require Application changes not covered by F3.
* Controller implementation would require repository or DbContext access.
* Authorization would need to be calculated in Web.
* Navigation mapping would need to be calculated in Web.
* Raw original HTML would need to be rendered without an existing safe rendering pattern.
* CSS changes cannot be located in an existing stylesheet.
* A JavaScript framework would be required.
* A migration appears necessary.
* Current source files conflict with the master RS-F source document.
* Existing comments or read events would need mandatory anchor data.

---

## Out of Scope

Do not implement:

* retrieval logic
* navigation mapping logic
* canonicalisation logic
* relocation
* fuzzy matching
* AI matching
* human relink
* rejection
* author insight
* sync/import changes
* schema changes
* comment redesign
* reader progress changes
* inline comment redesign

---

## Definition of Done

F3 is complete only when:

* Users can inspect original context for anchored comments.
* Non-anchored comments do not show original context actions.
* Original and current content are clearly distinguished.
* Original version metadata is shown where available.
* Legacy fallback is clearly identified.
* Navigation success and navigation fallback are both rendered safely.
* Unauthorized access is rejected.
* Missing anchor and missing original content are handled safely.
* Web uses Application service only.
* No repository or DbContext access has been added to Web.
* No inline styles have been added.
* CSS version is bumped if CSS changed.
* No schema migration has been added.
* No unrelated files are changed.
* Phase tests pass.
* Required broader tests pass, or the exact reason they could not be run is documented.
* `git diff --check` passes.
* Changes are committed on `RS-F-base/phase-f3-ui-integration`.

---

## Final Instruction

Be precise, conservative, and architecture-led.

Do not infer contracts.

Do not optimise for speed.

This phase succeeds only when original context is accessible through the UI, clearly labelled as historical original content, and implemented without Web-layer business logic.

```
```
