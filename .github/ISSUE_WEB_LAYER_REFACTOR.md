# Refactor: Extract business logic from Web layer controllers

## Problem

The Web layer contains **~530 lines of business logic** that violates architectural boundaries defined in `.github/copilot-instructions.md`.

**Violations found:**
- 🔴 9 critical violations (nested loops, domain mutations, complex orchestration)
- 🟡 3 moderate violations (helper methods with business logic)

**Controllers affected:**
- `AuthorController`: 7 critical violations (~350 lines of business logic)
- `BaseReaderController`: 2 critical violations (~180 lines of business logic)

## Architectural Standard Violation

From `.github/copilot-instructions.md`:

> Controllers must never contain: **loops over domain entities, repository calls, domain mutations, branching business rules, or multi-step orchestration.**

## Top 3 Most Severe Issues

### 1. `AuthorController.Sections` (lines 289-356)
- **3 nested foreach loops** over domain entities
- HTML diff computation in controller
- Change classification business logic
- **Should be:** `ISectionManagementService.GetSectionsSummaryAsync(projectId)`

### 2. `AuthorController.AddProjects` (lines 985-1077)
- **93 lines of orchestration code**
- 3 nested foreach loops
- Direct domain entity creation
- Duplicate `Task.Run` sync orchestration
- **Should be:** `IProjectManagementService.AddDiscoveredProjectsAsync(selectedUuids, author.Id)`

### 3. `BaseReaderController.BuildCommentDisplayModelsAsync` (lines 263-355)
- **93-line "helper method"** that's actually a full application service
- 4 foreach loops with nested async calls
- Complex permission determination logic
- **Should be:** `ICommentDisplayService.GetCommentDisplayDataAsync(commentIds, currentUserId, projectAuthorId)`

## Solution: 4-Phase Refactoring Plan

### Phase 1: Extract Most Critical Orchestration ⚡ START HERE
**Goal:** Extract the three largest business logic blocks to Application layer services

**Tasks:**
- [ ] Create `ISectionManagementService` interface
- [ ] Implement `SectionManagementService.GetSectionsSummaryAsync(projectId)` 
  - Includes publishability determination
  - Includes change classification computation
  - Returns complete DTO with all metadata
  - Full TDD: write failing tests first
- [ ] Update `AuthorController.Sections` to call service (thin layer)
- [ ] Create `IProjectManagementService` interface
- [ ] Implement `ProjectManagementService.AddDiscoveredProjectsAsync(selectedUuids, authorId)`
  - Handles soft-delete restore vs. new creation
  - Orchestrates background sync
  - Full TDD: write failing tests first
- [ ] Update `AuthorController.AddProjects` to call service
- [ ] Create `ICommentDisplayService` interface  
- [ ] Implement `CommentDisplayService.GetCommentDisplayDataAsync(...)`
  - Handles comment filtering, grouping, author name resolution
  - Handles passage anchor resolution
  - Full TDD: write failing tests first
- [ ] Update `BaseReaderController.BuildCommentDisplayModelsAsync` to call service
- [ ] Run full test suite: **zero regressions**

**Acceptance criteria:**
- All business logic moved to Application layer
- Controllers reduced to 5-step pattern: resolve user → validate → call service → map to VM → return
- 100% test coverage for new services
- Zero test failures

---

### Phase 2: Standardize Background Sync
**Goal:** Remove duplicate `Task.Run` orchestration blocks

**Tasks:**
- [ ] Create `ISyncOrchestrationService` interface
- [ ] Implement `SyncOrchestrationService.StartSyncAsync(projectId, authorId)`
  - Handles project state update (MarkSyncing)
  - Manages background scope creation
  - Handles error state persistence
  - Full TDD
- [ ] Replace `Task.Run` block in `AuthorController.Sync` (lines 111-141)
- [ ] Replace `Task.Run` block in `AuthorController.AddProjects` (lines 1039-1069)
- [ ] Run full test suite: **zero regressions**

**Acceptance criteria:**
- Single sync orchestration service
- No inline `Task.Run` blocks in controllers
- Consistent error handling for sync failures

---

### Phase 3: Extract Domain Mutations
**Goal:** Remove all direct domain entity mutations from controllers

**Tasks:**
- [ ] Implement `IProjectManagementService.SetActiveProjectAsync(projectId, authorId)`
  - Handles "only one active project" business rule
  - Replaces lines 172-180 in `AuthorController.ActivateProject`
  - Full TDD
- [ ] Implement `IVersioningService.DeleteVersionAsync(versionId, authorId)`
  - Enforces "cannot delete current version" business rule
  - Replaces lines 627-636 in `AuthorController.DeleteVersion`
  - Full TDD
- [ ] Implement `IProjectManagementService.SoftDeleteProjectAsync(projectId, authorId)`
  - Replaces lines 949-950 in `AuthorController.RemoveProject`
  - Full TDD
- [ ] Update all affected controller actions
- [ ] Run full test suite: **zero regressions**

**Acceptance criteria:**
- No direct entity mutations in controllers (no `project.MarkSyncing()`, `project.ActivateForReaders()`, etc.)
- All business rules enforced in Application layer
- Full test coverage

---

### Phase 4: Extract Helper Orchestration
**Goal:** Move remaining helper methods to Application services

**Tasks:**
- [ ] Implement `IReaderManagementService.GetReadersSummaryAsync(authorId)`
  - Replaces lines 735-755 in `AuthorController.Readers`
  - Full TDD
- [ ] Implement `ISectionManagementService.GetSectionDetailAsync(sectionId, authorId)`
  - Replaces lines 870-898 in `AuthorController.Section`
  - Full TDD
- [ ] Create `IContentNavigationService` interface
- [ ] Move tree-building helpers from `BaseReaderController`:
  - `BuildContentGroups` → `IContentNavigationService.BuildContentGroupsAsync`
  - `HasPublishedChapter` → `IContentNavigationService.HasPublishedChapterAsync`
  - `GetTopLevelAncestor` → `IContentNavigationService.GetTopLevelAncestorAsync`
  - `BuildBreadcrumb` → `IContentNavigationService.BuildBreadcrumbAsync`
  - Full TDD for each
- [ ] Update all controller usages
- [ ] Run full test suite: **zero regressions**

**Acceptance criteria:**
- No helper methods with business logic in controllers
- All orchestration in Application layer
- Controllers follow strict 5-step pattern

---

## Testing Requirements

Per `.github/copilot-instructions.md`:

**TDD Sequence (MANDATORY):**
1. Create empty stub with `throw new NotImplementedException()`
2. Write failing tests that prove the requirement
3. Implement to make tests pass
4. Run full test suite — zero regressions before proceeding
5. Refactor with tests green throughout

**Never write production code before a failing test exists for it.**

---

## Implementation Notes

- Deploy after each phase completes (avoid big-bang releases)
- Maintain backward compatibility during refactoring
- No UI changes — this is pure internal restructuring
- Update `REFACTORING.md` section 3.6 with progress
- Each phase should take 1-2 days maximum

---

## Impact

### Before (Current State)
- 🔴 Business logic scattered across controllers (untested)
- 🔴 Layer boundaries violated throughout
- 🔴 Maintenance burden increases with each feature
- 🔴 Cannot test business rules in isolation

### After (Target State)
- ✅ All business logic testable via Application service tests
- ✅ Clean layered architecture enforced
- ✅ Single source of truth for business rules
- ✅ Controllers follow documented 5-step pattern
- ✅ Easier to maintain and extend

---

## Detailed Analysis

See **`Web-Layer-Business-Logic-Review.md`** for:
- Complete code examples of each violation
- Full list of all 12 violations with severity ratings
- Line-by-line breakdown of problematic methods

---

## Priority

🔴 **HIGH PRIORITY**

This technical debt:
- Blocks maintainability
- Violates documented architecture standards  
- Makes testing difficult
- Increases risk of bugs with each change

**Recommendation:** Address Phase 1 before starting MT-Sprint or S-Sprint work.

---

## Related Files

- `.github/copilot-instructions.md` (controller standards)
- `REFACTORING.md` section 3.6
- `Web-Layer-Business-Logic-Review.md` (detailed analysis)
- `PRINCIPLES.md` (layering principles)

---

## Labels

- `refactoring`
- `technical-debt`
- `architecture`
- `high-priority`
