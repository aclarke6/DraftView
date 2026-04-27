````markdown
# RS-G Phase G1 - AI Candidate Integration

## Execution Mode

Cloud Execution Phase.

Apply the **Test Execution Override - Cloud Phases** rules from `AGENTS.md`.

This phase is non-deployable and inactive by design.

AI must not influence user-facing behaviour in this phase.

---

## Objective

Integrate AI candidate generation into the relocation pipeline without activating it.

AI must:

- receive bounded scene-level comparison data only
- return a candidate proposal
- never persist or affect anchor state
- never influence current match selection

This phase prepares the pipeline only.

---

## Deployability

**Deployable:** No  
**Must be deployed with:** G2 and G3

---

## Required Behaviour

### 1. Pipeline Position

AI integration must be placed after deterministic relocation failure only.

Deterministic pipeline:

1. Exact
2. Context
3. Fuzzy

AI must only execute when no deterministic match exists, or deterministic confidence is below the configured AI activation threshold.

AI must never run before deterministic steps.

AI must never run as a fallback for a successful deterministic match.

---

### 2. AI Input Contract

AI input must be limited to the original anchor context and the relevant source/target scene version content required for comparison.

AI must never receive:

- whole project manuscript content
- unrelated sections
- comments
- reader data
- author metadata
- project metadata not required for matching

Allowed input:

```csharp
public sealed class AiRelocationInput
{
    public string OriginalSelectedText { get; init; } = string.Empty;
    public string NormalizedSelectedText { get; init; } = string.Empty;
    public string PrefixContext { get; init; } = string.Empty;
    public string SuffixContext { get; init; } = string.Empty;

    public string? OriginalSceneCanonicalText { get; init; }
    public string TargetSceneCanonicalText { get; init; } = string.Empty;

    public int? OriginalStartOffset { get; init; }
    public int? OriginalEndOffset { get; init; }
}
````

Rules:

* `TargetSceneCanonicalText` must be one relevant target scene version.
* `OriginalSceneCanonicalText` may be included only when needed to compare old scene context against new scene context.
* All text must be canonical text.
* Input must be deterministic and reproducible.
* No HTML should be passed to AI unless a current implementation proves canonical text is insufficient and the stop condition is reported first.

---

### 3. AI Output Contract

AI must return a proposal only.

```csharp
public sealed class AiRelocationProposal
{
    public bool HasCandidate { get; init; }

    public int? StartOffset { get; init; }
    public int? EndOffset { get; init; }

    public string MatchedText { get; init; } = string.Empty;

    public int ConfidenceScore { get; init; }

    public string Rationale { get; init; } = string.Empty;
}
```

Rules:

* Offsets must be canonical offsets in `TargetSceneCanonicalText`.
* `ConfidenceScore` must be between 0 and 100.
* `MatchedText` must be the proposed canonical target text.
* `Rationale` must be short and bounded.
* AI must not claim certainty.
* AI output must have no side effects.

---

### 4. Inactive Behaviour

In G1, AI output must not:

* update `PassageAnchor.CurrentMatch`
* update `PassageAnchor.Status`
* persist an active match
* influence returned active match
* influence UI

AI output may be returned internally as a proposal for tests and future G2/G3 integration.

---

### 5. Service Contract

Add an Application-level abstraction unless an equivalent existing abstraction is already present:

```csharp
public interface IAiRelocationService
{
    Task<AiRelocationProposal> ProposeAsync(
        AiRelocationInput input,
        CancellationToken cancellationToken = default);
}
```

Rules:

* Must be mockable.
* Must be called only by Application relocation orchestration.
* Must not be called from Web.
* Must not be referenced by Domain.
* Infrastructure may provide the eventual implementation, including Ollama.

Do not hard-code Ollama into Application.

---

### 6. Orchestration Contract

Extend the existing relocation orchestration with an inactive AI proposal path.

Required semantics:

```text
Run deterministic relocation.
If deterministic relocation succeeds above threshold, return deterministic result.
If deterministic relocation fails or is below AI activation threshold, request AI proposal.
Do not persist the AI proposal in G1.
Do not convert the AI proposal into CurrentMatch in G1.
Return existing orphan or deterministic outcome unchanged.
```

---

## Architecture Constraints

* Domain must not depend on AI.
* Application owns AI orchestration.
* Infrastructure owns external AI implementation.
* Web must not call AI directly.
* No schema changes.
* No migrations.
* No persistence changes.
* No UI changes.
* Sync/import code must not call AI.

---

## TDD Requirements

Follow strict TDD:

1. Create stub with `NotImplementedException` where new production types are required.
2. Write failing tests.
3. Confirm failure reason.
4. Implement minimal logic.
5. Refactor only after tests are green.

---

## Required Tests

### AI is not called before deterministic failure

Given deterministic relocation succeeds above threshold
When relocation runs
Then AI is not called.

### AI is called after deterministic failure

Given exact, context, and fuzzy relocation fail
When relocation runs
Then AI proposal service is called.

### AI receives bounded scene-level input

Given relocation requires AI
When AI is invoked
Then input contains only original anchor context and relevant scene canonical text
And does not contain project manuscript content, unrelated scenes, comments, reader data, author metadata, or project metadata.

### AI proposal does not persist

Given AI returns a candidate
When relocation completes in G1
Then `PassageAnchor.CurrentMatch` is unchanged
And `PassageAnchor.Status` is unchanged.

### AI proposal is inactive

Given AI returns a candidate
When relocation completes in G1
Then the active relocation result remains the same result that would have been returned without AI.

### AI failure degrades safely

Given AI throws or fails
When relocation runs
Then no exception escapes
And the result remains orphan or the existing deterministic outcome.

### Domain remains AI-free

Given the solution is inspected
Then Domain contains no dependency on `IAiRelocationService`, Ollama, AI scoring clients, or AI prompt types.

---

## Stop Conditions

Stop immediately if:

* AI requires whole project manuscript content.
* AI requires unrelated scenes.
* AI requires comments, reader data, author metadata, or project metadata.
* AI must be called before deterministic relocation completes.
* AI output would be persisted in G1.
* AI output would influence active match selection in G1.
* Domain layer would require an AI dependency.
* Web layer would need to call AI directly.
* No mockable/testable abstraction can be introduced.
* Existing relocation pipeline cannot expose deterministic failure without changing unrelated behaviour.

---

## Definition of Done

This phase is complete only when:

* AI proposal can be generated after deterministic failure.
* AI receives bounded scene-level comparison input only.
* AI does not affect anchor state.
* AI does not affect active relocation outcome.
* Domain remains AI-free.
* Web remains AI-free.
* No schema changes exist.
* No UI changes exist.
* Phase tests pass.
* Required broader tests pass, or the exact reason they could not be run is documented.
* `git diff --check` passes.
* Changes are committed on `RS-G-base/phase-g1-integration`.

---

## Final Instruction

AI exists in this phase as a silent advisor only.

It must not decide, persist, or influence outcomes.

Only propose.

```
```
