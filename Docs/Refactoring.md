# DraftView — Refactoring Principles and Standards

This document defines the refactoring rules, commenting standards, and code hygiene
principles that apply to all DraftView development. These rules are permanent and
apply to every sprint, every change, and every layer of the solution.

The incremental refactor roadmap (controller extraction, startup decomposition, etc.)
is tracked in TASKS.md. This document contains timeless principles only.

---

## 1. Rename Completeness

A rename is not complete at the class boundary.

When an entity, service, interface, or concept is renamed, audit and update every
artefact whose name was derived from the old concept:

- Class names and file names
- Method names
- Interface names
- DTOs and ViewModels
- Controller actions and route names
- Razor view files
- CSS classes (where the name derived from the concept)
- JavaScript identifiers
- EF configuration and migration names (new migrations only — existing migration history is immutable)
- Test class names and test method names
- Code comments
- XML doc summaries
- Inline documentation

**The completeness test:** a solution-wide search for the old term returns zero hits outside
of migration history and `[DONE]` entries in project documents. Migration history and
historical task records are immutable and are the only permitted exceptions.

**The exception for behaviour-specific names:** artefacts that describe their own distinct
behaviour rather than deriving their name from the renamed concept retain their names.
A CSS class `.notification-badge` is not renamed just because `UserNotificationSettings`
became `UserPreferences` — it describes a visual behaviour that still exists. The rule is:
rename what derived its name from the concept; keep what describes its own distinct behaviour.

---

## 2. Method Length and Extraction

**Over 20 lines:** candidate for extraction. Review whether the method has more than one
responsibility. Extract if it does.

**Over 30 lines:** extraction is mandatory unless the method is genuinely atomic — that is,
it performs one indivisible operation that cannot be meaningfully split without obscuring
intent. An atomic exception must be self-evident from reading the method. If it requires
justification, it is not atomic.

**The extraction rule:** if you have to read more than a screenful to understand what a
method does, it has too many concerns. Extract named helper methods that reveal intent.
The method name is the comment.

**On extraction:** extracted helper methods must be named to describe what they do, not
how they do it. `ValidateSection` is a name. `CheckIfSectionIsNotNullAndNotDeletedAndIsDocument`
is not.

---

## 3. No Magic Numbers or Strings

Any literal that appears more than once, or whose meaning is not self-evident in context,
becomes a named constant or enum value.

A string like `"Author"` used as a role name throughout the application is a magic string.
A number like `90` used as a notification retention period in days is a magic number.
Both must be named constants.

A literal `1` used as a default sort order seed in a single method is self-evident in context
and does not require extraction.

---

## 4. Dead Code

Commented-out code, unused methods, unreachable branches, and unused parameters are deleted —
not left "just in case."

Git is the history. If code might be needed again, it can be retrieved from version history.
Dead code is noise that erodes confidence in the live code and makes the next fault harder to find.

---

## 5. Refactor as a Separate Commit

Never mix a refactor with a behaviour change in the same commit.

If improving structure and fixing a bug in the same sitting: commit the refactor first
(tests green), then the fix. This keeps the diff reviewable and git blame meaningful.

Within a sprint phase branch, a refactor discovered during feature work is committed
separately before the feature commit. It does not require a separate branch unless it
is a planned whole-class refactor (see section 7).

---

## 6. No Refactor Without Green Tests

A refactor that breaks tests was a behaviour change in disguise.

If tests go red during a refactor, stop immediately. Understand why before proceeding.
Do not suppress or skip tests to make a refactor appear clean.

The full test suite must be green at every refactor commit — not just the tests directly
related to the changed code.

---

## 7. The Boy Scout Rule

Leave the code slightly cleaner than you found it.

**Scope:** the boy scout rule applies only to the current change — the method being added
or edited and its immediate context. It does not extend to the whole class unless that
class has been scheduled for a planned refactor.

**Planned whole-class refactor:** when a class warrants a full refactor, it is scheduled
as an explicit planned item. The scope of a planned whole-class refactor includes the
class itself, its helper methods, extension methods, and all comments. It is committed
as a dedicated refactor commit with a clear message before any feature work in that session.

---

## 8. ViewModel and DTO Hygiene

Every property on a ViewModel must be bound in its view.
Every property on a DTO must be used by its consumer.

**Trigger:** audit the ViewModel or DTO whenever you touch the view or consumer it belongs to.
Remove unused properties at that point. Do not defer to a later cleanup pass.

A ViewModel property added for a feature that was subsequently removed or redesigned is
dead code. It is deleted, not left in place.

---

## 9. Commenting Standards

### 9.1 Classes

Every class requires an XML summary comment describing its responsibility.

```csharp
/// <summary>
/// Orchestrates the republish workflow, creating a SectionVersion snapshot
/// for each Document descendant of the target chapter.
/// </summary>
public class VersioningService : IVersioningService
```

### 9.2 Methods

Methods over 5 lines require an XML summary comment.

Methods 5 lines or under that are atomic — performing one indivisible operation — do not
require a comment. The method name is sufficient.

```csharp
/// <summary>
/// Creates a new SectionVersion for each non-soft-deleted Document section
/// that is a descendant of the given chapter. Sets IsPublished on each versioned section.
/// Throws if the chapter has no publishable Document descendants.
/// </summary>
public async Task RepublishChapterAsync(int chapterId, int authorId, CancellationToken ct)
```

### 9.3 Test Classes

Every test class requires an XML summary comment stating explicitly:
- What behaviour is under test
- What is deliberately out of scope for this test class

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

### 9.4 Test Methods

A test method does not require a summary comment if its name fully communicates intent
using the `{Method}_{Condition}_{ExpectedOutcome}` convention.

A test method requires a summary comment when either:
- The method name alone does not convey the full intent
- The setup is non-obvious — a specific domain state or seeding sequence that is not
  apparent from the test name alone

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

## 10. Architecture Boundary Enforcement

Refactoring must never move code across architecture boundaries as a side effect.

If a refactor of an application service reveals that some logic belongs in the domain,
that is a domain change — it requires its own TDD sequence (stub, failing test, implement)
and its own commit, not a silent move during a refactor.

Boundaries:
- Domain logic stays in Domain
- Orchestration stays in Application
- Persistence stays in Infrastructure
- HTTP concerns stay in Web

A refactor that silently crosses a boundary is a behaviour change, not a refactor.

## 11. Using Statements

Using statements are ordered alphabetically by namespace, with `System` namespaces first.

- Any unused using statements are removed as part of the refactor. The presence of an unused using is a sign that the refactor is incomplete.
- Any new using statements introduced by the refactor are added in the same commit as the code that requires them, not deferred to a later cleanup commit.
- Any redundant domain namespace qualifiers on variables, methods, or classes are removed where a using statement already covers the namespace. For example, if `using DraftView.Domain.Entities` is present, a variable declaration like `DraftView.Domain.Entities.Section section` is redundant and must be simplified to `Section section`.