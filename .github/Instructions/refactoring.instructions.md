---
applyTo: "**"
---

# DraftView — Refactoring Instructions

Full refactoring principles and standards in `REFACTORING.md`.
This file distils those rules into the form Copilot needs when writing or modifying code.

---

## Rename Completeness

A rename is not complete at the class boundary.

When renaming any entity, service, interface, or concept, update every derived artefact:

- Class names and file names
- Method names and parameter names
- Interface names
- DTOs and ViewModels
- Controller actions and route names
- Razor view files
- CSS classes derived from the concept
- JavaScript identifiers
- EF configuration classes
- New migration names only — existing migration history is immutable
- Test class names and test method names
- Code comments and XML doc summaries

**Completeness test:** solution-wide search for the old term returns zero hits outside
migration history and `[DONE]` entries in project documents.

**Exception:** artefacts that describe their own distinct behaviour retain their names.
A CSS class `.notification-badge` is not renamed because `UserNotificationSettings`
became `UserPreferences`. Rename what derived its name from the concept. Keep what
describes its own behaviour.

---

## Method Length

| Length | Rule |
|--------|------|
| Under 20 lines | No action required |
| Over 20 lines | Review for single responsibility. Extract if more than one concern exists |
| Over 30 lines | Extraction mandatory unless the method is genuinely atomic |

**Atomic exception:** a method that performs one indivisible operation may exceed 30 lines.
The exception must be self-evident from reading the method. If it requires justification,
it is not atomic.

**Extracted helper names** describe what they do, not how they do it.
`ValidateSection` is correct. `CheckIfSectionIsNotNullAndNotDeletedAndIsDocument` is not.

---

## No Magic Numbers or Strings

Any literal that appears more than once, or whose meaning is not self-evident in context,
becomes a named constant or enum value.

- Role name strings → named constants
- Retention periods, limits, thresholds → named constants
- A literal `1` used once as a default seed in a single method → self-evident, no extraction needed

---

## Dead Code

Delete: commented-out code, unused methods, unreachable branches, unused parameters.

Never leave code "just in case." Git is the history.

---

## Refactor Commits

Never mix a refactor with a behaviour change in the same commit.

- Refactor first, tests green, commit
- Then make the behaviour change, commit separately
- Within a phase branch: refactor is a separate commit before the feature commit
- A planned whole-class refactor is a dedicated commit with a `refactor:` prefix before
  any feature work in that session

---

## No Refactor Without Green Tests

If tests go red during a refactor, stop. Understand why before proceeding.
A refactor that breaks tests was a behaviour change in disguise.
Full test suite must be green at every refactor commit.

---

## Boy Scout Rule

Leave the code slightly cleaner than you found it.

**Scope:** the method being added or edited and its immediate context only.
Does not extend to the whole class unless a whole-class refactor has been explicitly planned.

**Planned whole-class refactor** includes: the class, its helper methods, extension methods,
and all comments. Committed as a dedicated `refactor:` commit.

---

## ViewModel and DTO Hygiene

Every property on a ViewModel must be bound in its view.
Every property on a DTO must be used by its consumer.

**Trigger:** audit the ViewModel or DTO whenever you touch the view or consumer it belongs to.
Remove unused properties immediately. Do not defer.

---

## Commenting Standards

### Classes

Every class requires an XML summary comment.

```csharp
/// <summary>
/// Orchestrates the republish workflow, creating a SectionVersion snapshot
/// for each Document descendant of the target chapter.
/// </summary>
public class VersioningService : IVersioningService
```

### Methods

Methods over 5 lines require an XML summary comment.
Methods 5 lines or under that are atomic do not require a comment — the name is sufficient.

```csharp
/// <summary>
/// Creates a new SectionVersion for each non-soft-deleted Document section
/// that is a descendant of the given chapter. Sets IsPublished on each versioned section.
/// Throws if the chapter has no publishable Document descendants.
/// </summary>
public async Task RepublishChapterAsync(int chapterId, int authorId, CancellationToken ct)
```

### Test Classes

Every test class requires an XML summary stating what is in scope and what is explicitly
out of scope.

```csharp
/// <summary>
/// Tests for VersioningService.RepublishChapterAsync.
/// Covers: version creation per document, version number sequencing,
/// soft-deleted document exclusion, invariant violations.
/// Excludes: notification dispatch (NotificationServiceTests),
/// reader view resolution (covered in Web layer tests).
/// </summary>
public class VersioningServiceTests
```

### Test Methods

No summary required when the `{Method}_{Condition}_{ExpectedOutcome}` name is self-explanatory
and the setup is straightforward.

Summary required when:
- The method name does not fully convey the intent
- The setup is non-obvious — a specific domain state or seeding sequence not apparent
  from the name alone

```csharp
/// <summary>
/// Verifies that a section soft-deleted between the chapter load and the per-document
/// iteration is skipped rather than causing an exception. Requires two saves to simulate
/// a concurrent soft-delete mid-republish.
/// </summary>
[Fact]
public async Task RepublishChapterAsync_WithConcurrentSoftDelete_SkipsDeletedDocument()
```

---

## Architecture Boundary Enforcement

Refactoring must never move code across architecture boundaries as a side effect.

If a refactor reveals that logic belongs in a different layer, that is a separate change —
it requires its own TDD sequence and its own commit.

| Layer | Owns |
|-------|------|
| Domain | Entities, invariants, factory methods, domain services |
| Application | Orchestration, interfaces, DTOs |
| Infrastructure | EF Core, repositories, encryption, email, Dropbox |
| Web | Controllers, Razor views, ViewModels — HTTP concerns only |

A refactor that silently moves logic across a boundary is a behaviour change, not a refactor.