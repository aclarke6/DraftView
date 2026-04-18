---
mode: agent
description: V-Sprint 5 Phase 1 — AI Summary Service
---

# V-Sprint 5 / Phase 1 — AI Summary Service

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `Publishing And Versioning Architecture.md` sections 4.3 and V-Sprint 5
2. Read `REFACTORING.md` in full
3. Read `.github/copilot-instructions.md`
4. Read `DraftView.Domain/Entities/SectionVersion.cs` — `AiSummary` property already exists
5. Read `DraftView.Application/Services/VersioningService.cs` — understand the publish flow
6. Read `DraftView.Domain/Interfaces/Services/IChangeClassificationService.cs` — pattern to follow
7. Confirm the active branch is `vsprint-5--phase-1-ai-summary-service`
   — if not on this branch, stop and report
8. Run `git status` — confirm the working tree is clean with no uncommitted changes.
   If uncommitted changes exist that are not part of this phase, stop and report.
9. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Goal

Introduce `IAiSummaryService` — an application-layer service that generates a
one-line AI summary describing what changed between the previous version and the
current working state.

The summary names characters, locations, and events from the prose. It is written
in the author's voice as a note to beta readers. It is never generic ("content was
updated"). It is never a diff description ("paragraph 3 was changed").

For first-version sections (no previous `SectionVersion`): summarise what the
section introduces, not what changed.

The service calls the Anthropic API. AI failure must never block publishing.
The service returns `null` on any failure — the caller handles the null case gracefully.

---

## TDD Sequence

The AI service involves external HTTP calls. Tests must use a mock of `IAiSummaryService`
at the integration points. The service implementation itself is tested via unit tests
that mock the HTTP client or use a test double for the API call.

For the interface and domain method, TDD applies as normal.
For the service implementation, test the orchestration logic and null-on-failure
behaviour using mocked HTTP responses.

---

## Existing Patterns — Follow These Exactly

- Application service interfaces in `DraftView.Domain/Interfaces/Services/`
- Application service implementations in `DraftView.Application/Services/`
- Tests in `DraftView.Application.Tests/Services/`
- `SectionVersion.AiSummary` already exists as a nullable string property
- XML summary on every class and method
- No magic strings — prompt text is a named constant or extracted method

---

## Deliverable 1 — `SectionVersion.SetAiSummary`

**File:** `DraftView.Domain/Entities/SectionVersion.cs`

Add a domain method to set `AiSummary` after creation:

```csharp
/// <summary>
/// Sets the AI-generated summary for this version.
/// Called by the application layer after AI summary generation during Republish.
/// Can only be set once — summary is immutable after first assignment.
/// </summary>
/// <param name="summary">The one-line summary to assign. Must not be null or whitespace.</param>
/// <exception cref="InvariantViolationException">Thrown when summary has already been set or is empty.</exception>
public void SetAiSummary(string summary)
{
    if (AiSummary is not null)
        throw new InvariantViolationException("I-VER-AISUMMARY",
            "AiSummary has already been set and cannot be changed.");

    if (string.IsNullOrWhiteSpace(summary))
        throw new InvariantViolationException("I-VER-AISUMMARY-EMPTY",
            "AiSummary must not be null or whitespace.");

    AiSummary = summary;
}
```

### Domain Tests

**File:** `DraftView.Domain.Tests/Entities/SectionVersionTests.cs`

Add to the existing test class:

```
SetAiSummary_SetsSummary
SetAiSummary_WhenAlreadySet_ThrowsInvariantViolation
SetAiSummary_WithEmptySummary_ThrowsInvariantViolation
SetAiSummary_WithWhitespaceSummary_ThrowsInvariantViolation
Create_HasNullAiSummary
```

Run full test suite. Zero regressions.
Commit: `domain: add SetAiSummary to SectionVersion`

---

## Deliverable 2 — `IAiSummaryService` Interface

**File:** `DraftView.Domain/Interfaces/Services/IAiSummaryService.cs`

```csharp
namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Generates a one-line AI summary describing changes between versions.
/// The summary names characters, locations, and events from the prose.
/// Written in the author's voice as a note to beta readers.
/// Returns null on any failure — callers must handle null gracefully.
/// AI failure must never block publishing.
/// </summary>
public interface IAiSummaryService
{
    /// <summary>
    /// Generates a one-line summary for a section version.
    /// </summary>
    /// <param name="previousHtml">The previous version's HTML content. Null for first versions.</param>
    /// <param name="currentHtml">The current working state HTML content.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A one-line summary string, or null if generation failed or was skipped.</returns>
    Task<string?> GenerateSummaryAsync(
        string? previousHtml,
        string currentHtml,
        CancellationToken ct = default);
}
```

---

## Deliverable 3 — `AiSummaryService` Implementation

**File:** `DraftView.Application/Services/AiSummaryService.cs`

Implements `IAiSummaryService`.

### Configuration

The service requires the Anthropic API key from configuration. Read it from
`IConfiguration` under the key `"Anthropic:ApiKey"`. If the key is missing or empty,
return `null` immediately without attempting a network call.

```csharp
public class AiSummaryService(IConfiguration configuration) : IAiSummaryService
```

### Prompt Construction

Extract prompt construction into a private method `BuildPrompt(string? previousHtml, string currentHtml)`.

**For first versions** (when `previousHtml` is null or empty):

```
You are helping a fiction author communicate with their beta readers.

The author has just published a new section. Write a single sentence (maximum 2 sentences)
that tells the beta readers what this section introduces. Name the characters, locations,
and events that appear. Write in the author's voice — warm, direct, and specific.
Never write generic phrases like "content was added" or "a new section is available".

Section content:
{currentHtml with HTML tags stripped}

Respond with only the summary sentence. No preamble, no explanation.
```

**For subsequent versions** (when `previousHtml` has content):

```
You are helping a fiction author communicate with their beta readers.

The author has revised a section. Write a single sentence (maximum 2 sentences)
that tells the beta readers what changed. Name the characters, locations, and events
that were added, changed, or removed. Write in the author's voice — warm, direct, and specific.
Never write generic phrases like "content was updated" or "changes were made".

Previous version:
{previousHtml with HTML tags stripped}

Revised version:
{currentHtml with HTML tags stripped}

Respond with only the summary sentence. No preamble, no explanation.
```

### HTML Stripping Helper

```csharp
private static string StripHtml(string html)
    => System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ")
        .Replace("  ", " ").Trim();
```

### API Call

Use `HttpClient` to call the Anthropic API. The model is `claude-sonnet-4-20250514`.
Max tokens: 150 (one-line summary only).

```csharp
private static readonly string ApiUrl = "https://api.anthropic.com/v1/messages";
private static readonly string ModelId = "claude-sonnet-4-20250514";
private const int MaxSummaryTokens = 150;
```

The request body:
```json
{
  "model": "claude-sonnet-4-20250514",
  "max_tokens": 150,
  "messages": [
    { "role": "user", "content": "<prompt>" }
  ]
}
```

Headers required:
- `x-api-key`: the API key from configuration
- `anthropic-version`: `2023-06-01`
- `Content-Type`: `application/json`

Parse the response: extract `content[0].text` from the JSON response.

### Null-on-Failure Contract

Wrap the entire implementation in try/catch. Any exception — network failure,
API error, JSON parse error, timeout — returns `null`. Never throw from this service.

```csharp
public async Task<string?> GenerateSummaryAsync(
    string? previousHtml,
    string currentHtml,
    CancellationToken ct = default)
{
    try
    {
        // ... implementation
    }
    catch
    {
        return null;
    }
}
```

### Tests — `DraftView.Application.Tests/Services/AiSummaryServiceTests.cs`

These tests should not make real HTTP calls. Use `IHttpClientFactory` injection
or a constructor that accepts `HttpClient` directly for testability.

If the implementation uses `IHttpClientFactory`, inject a mock. If it uses
`HttpClient` directly via constructor injection, pass a `HttpClient` backed
by a `MockHttpMessageHandler` in tests.

Write all tests **failing** before implementing:

```
GenerateSummaryAsync_WithMissingApiKey_ReturnsNull
GenerateSummaryAsync_WithEmptyCurrentHtml_ReturnsNull
GenerateSummaryAsync_WhenApiThrows_ReturnsNull
GenerateSummaryAsync_WhenApiReturnsError_ReturnsNull
GenerateSummaryAsync_WithNoPreviousHtml_UsesFirstVersionPrompt
GenerateSummaryAsync_WithPreviousHtml_UsesRevisionPrompt
GenerateSummaryAsync_OnSuccess_ReturnsSummaryText
```

For HTTP mocking, use a simple `MockHttpMessageHandler` helper in the test project:

```csharp
public class MockHttpMessageHandler(HttpStatusCode statusCode, string content)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        });
}
```

Run full test suite. Zero regressions.
Commit: `app: add AiSummaryService calling Anthropic API for version summaries`

---

## Deliverable 4 — DI Registration

**File:** `DraftView.Web/Extensions/ServiceCollectionExtensions.cs`

Register `IAiSummaryService`:

```csharp
services.AddHttpClient<IAiSummaryService, AiSummaryService>();
```

Using `AddHttpClient` registers a typed `HttpClient` with proper lifecycle management.

Run `dotnet build --nologo` to confirm compilation.
Run `dotnet test --nologo` — full suite green.
Commit: `app: register AiSummaryService in DI`

---

## Phase Gate — All Must Pass Before Marking Complete

Run `dotnet test --nologo` and confirm:

- [ ] All new tests green
- [ ] Total passing count equal to or greater than baseline
- [ ] Solution builds without errors
- [ ] `SectionVersion.SetAiSummary` method exists
- [ ] `IAiSummaryService` exists in `DraftView.Domain/Interfaces/Services`
- [ ] `AiSummaryService` exists in `DraftView.Application/Services`
- [ ] API key read from `Anthropic:ApiKey` configuration — not hardcoded
- [ ] Null returned on any failure — never throws
- [ ] Prompt construction extracted to named private method
- [ ] HTML stripping helper extracted to named private method
- [ ] No magic strings — model ID and API URL are named constants
- [ ] `IAiSummaryService` registered via `AddHttpClient` in DI
- [ ] No controller changes
- [ ] No view changes
- [ ] No EF migration required (`AiSummary` column already exists on `SectionVersions`)
- [ ] TASKS.md Phase 1 checkbox updated to `[x]`
- [ ] All changes committed to `vsprint-5--phase-1-ai-summary-service`
- [ ] No warnings in test output linked to phase changes
- [ ] Refactor considered and applied where appropriate, tests green after refactor

---

## Identify All Warnings in Tests

Run `dotnet test --nologo` and identify any warnings in the test output.
Address any warnings that are linked to code changes made in this phase before
proceeding, as they may indicate potential issues in the code.

---

## Refactor Phase

After implementing the above, consider if any refactor is needed to improve code
quality, as per the refactoring guidelines. If so, perform the refactor and ensure
all tests still pass.

---

## Do NOT implement in this phase

- Calling `IAiSummaryService` from `VersioningService` — Phase 2
- Author editable summary textarea on Republish — Phase 2
- Reader banner showing the summary — Phase 3
- Any view or controller changes
