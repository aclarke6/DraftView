# DraftView — AI Scoring Service
Version: 0.1 | Date: 2026-04-20
Status: **Design** — not yet scheduled

---

## Overview

The AI Scoring Service analyses content changes between published versions and current drafts, producing a significance score (0–10) and a one-sentence summary of what changed. This helps authors decide whether a change warrants republishing and notifying readers.

The service is **agent and model agnostic** — the author can choose which AI model scores their changes, subject to their subscription tier. Different models offer different cost/accuracy trade-offs.

---

## Design Principles

- **Provider agnostic** — `IAiScoringProvider` abstraction; any model can be plugged in
- **Idempotent** — a score is computed once per change cycle; if already stored, no API call is made
- **Tier gated** — AI scoring is a paid feature; Free tier gets a limited taste
- **Background computed** — scoring happens in the background sync pipeline, not on page load; scores are ready when the author opens the Sections view
- **Diff-based** — the AI receives only the diff between the last published version and the current content, not the full text; keeps token usage and cost low
- **Cost transparent** — the author sees which model they are using and what their monthly allowance is

---

## Scoring Quota Model

| Tier | Monthly Scored Changes | Available Models |
|------|----------------------|-----------------|
| Free | 1 | Cheapest model only (e.g. Claude Haiku) — a taste to tempt the author |
| Paid | X (TBD — e.g. 50) | Author can choose: cheap model (included) or accurate model (additional fee per call) |
| Ultimate | XX (TBD — e.g. 500) | All models available; no unlimited — AI providers do not offer unlimited supply |

**Notes:**
- Quota resets monthly per Tenancy
- Unused quota does not roll over
- When quota is exhausted, the change indicator still shows but no score or summary is computed
- Free tier quota is deliberately low — enough to demonstrate value, not enough to rely on

---

## Available Models (initial set)

| Model | Provider | Tier | Cost Profile | Use Case |
|-------|----------|------|-------------|----------|
| Claude Haiku 4.5 | Anthropic | Free + Paid (included) | Cheapest | Fast, sufficient for simple scoring |
| Claude Sonnet 4.6 | Anthropic | Paid (additional fee) | Mid | More nuanced scoring, better summaries |
| Gemini Flash | Google | Paid (additional fee) | Cheap alternative | Fast, cheap — second provider option |
| GPT-4o mini | OpenAI | Paid (additional fee) | Cheap alternative | Fast, cheap — third provider option |

Model availability is configured server-side. New models are added without code changes to consuming services — only a new `IAiScoringProvider` implementation is needed.

**Per-author model selection:**
- Paid and Ultimate authors can select their preferred model in Account Settings
- The selection is stored as `PreferredAiScoringModel` on `TenancyMembership` (or `UserPreferences` pre-MT)
- Free authors have no choice — cheapest model only

---

## Domain Changes

### On `Section`:
- `ChangeSignificanceScore` — `int?` (0–10, null until scored)
- `ChangeSummary` — `string?` (one-sentence AI summary of what changed, null until scored)
- Both fields are cleared when `ContentChangedSincePublish` is reset to false (i.e. on republish)

### On `TenancySubscription` (MT-Sprint-2+):
- `AiScoringQuotaUsed` — `int` (resets monthly)
- `AiScoringQuotaResetAt` — `DateTime`

### On `UserPreferences` (interim, pre-MT):
- `PreferredAiScoringModel` — `string?` (null = use default cheapest model)

---

## `IAiScoringProvider` Abstraction

```csharp
public interface IAiScoringProvider
{
    string ModelName { get; }
    string ProviderId { get; }
    Task<AiScoringResult> ScoreChangeAsync(
        string diffHtml,
        string sectionTitle,
        CancellationToken ct = default);
}

public record AiScoringResult(
    int Score,           // 0-10
    string Summary,      // One sentence describing what changed
    string ModelUsed,    // For display and audit
    int TokensUsed);     // For cost tracking
```

Implementations:
- `AnthropicHaikuScoringProvider` — wraps `claude-haiku-4-5`
- `AnthropicSonnetScoringProvider` — wraps `claude-sonnet-4-6`
- `GeminiFlashScoringProvider` — wraps Gemini Flash (future)
- `OpenAiMiniScoringProvider` — wraps GPT-4o mini (future)

---

## `IAiScoringService` — Application Layer

```csharp
public interface IAiScoringService
{
    /// <summary>
    /// Scores a changed section if quota allows and score not already computed.
    /// No-op if quota exhausted or score already stored.
    /// </summary>
    Task ScoreIfEligibleAsync(Guid sectionId, Guid authorId, CancellationToken ct = default);
}
```

Called from:
- `ScrivenerSyncService` — after `ReconcileProjectFromScrivxAsync` detects content changes
- Specifically: for every section where `ContentChangedSincePublish` is newly set to true AND `ChangeSignificanceScore` is null

---

## Sections View UX

On the Sections page, for each changed chapter:

- **Change indicator** — always shown (no AI required)
- **Score badge** (0–10) — shown when score is available; spinner if being computed; dash if quota exhausted
- **Hover tooltip** — shows `ChangeSummary` when score available; "Upgrade to Paid for AI scoring" when Free quota exhausted
- **Republish button** — shown directly on the Sections page (fixes current navigation bug)
- **Last changed date** — shown alongside the change indicator

Author threshold slider (in Settings):
- Range 0–10
- "Only alert me when significance score is above X"
- Stored as `AiScoringAlertThreshold` on `UserPreferences`
- Used to highlight chapters above the threshold more prominently

---

## Sprint Plan

### AISprint-1 — Foundation
- [ ] `IAiScoringProvider` interface + `AnthropicHaikuScoringProvider` implementation
- [ ] `IAiScoringService` application service
- [ ] `ChangeSignificanceScore` and `ChangeSummary` on `Section` + EF migration
- [ ] Quota tracking on `UserPreferences` (interim) + monthly reset logic
- [ ] Integration into `ScrivenerSyncService` post-reconciliation
- [ ] Sections view: republish button on changed chapters + score badge + hover tooltip + last changed date
- [ ] Free tier quota enforcement (1 per month, Haiku only)

### AISprint-2 — Model Selection
- [ ] `PreferredAiScoringModel` on `UserPreferences`
- [ ] Author model selection UI in Account Settings (Paid/Ultimate only)
- [ ] `AnthropicSonnetScoringProvider` implementation
- [ ] Per-model cost tracking for future billing integration

### AISprint-3 — Multi-Provider (post-MT)
- [ ] `GeminiFlashScoringProvider`
- [ ] `OpenAiMiniScoringProvider`
- [ ] Provider selection UI updated with new models
- [ ] Quota model integrated with `TenancySubscription` (replaces `UserPreferences` interim)

### AISprint-4 — Advanced (post-revenue)
- [ ] Per-call billing for additional-fee models on Paid tier
- [ ] Usage dashboard for author (quota used, models used, tokens consumed)
- [ ] Quota rollover options (future tier feature)

---

## Open Design Questions

- [ ] What is the exact monthly quota for Paid and Ultimate tiers? (TBD — needs cost modelling)
- [ ] Should the quota count per section-change or per API call? (A chapter with 3 changed scenes = 3 calls or 1?)
- [ ] Should the diff be sent as HTML or plain text to the AI? (Plain text is cheaper in tokens)
- [ ] What is the prompt? Needs careful design — "score 0-10 how significantly this change affects a reader's experience of the story" is the intent
- [ ] Should `ChangeSummary` be from the author's perspective ("You removed a paragraph describing X") or neutral ("A paragraph describing X was removed")?
- [ ] Token usage audit — establish baseline token cost per average diff before setting quota limits

---

## Reference Documents
- `MultiTenancy.md` — subscription tier model, `TenancySubscription`
- `ScrivenerSync-BusinessModel-v3.docx` — invariants, service definitions
- `ScrivenerSync-BillingModel-v2.docx` — tier definitions
