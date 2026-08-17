# Web Layer Business Logic Review
**Date:** 2026-08-17  
**Reviewer:** AI Assistant  
**Status:** 🔴 **SIGNIFICANT VIOLATIONS FOUND**

---

## Executive Summary

The Web layer contains **substantial business logic leakage** that violates the architectural boundaries defined in `.github/copilot-instructions.md`. Controllers are performing multi-step orchestration, domain entity mutations, complex data transformations, and branching business rules that belong in the Application or Domain layers.

**Severity:** HIGH  
**Impact:** Maintenance burden, testability issues, architectural drift, potential future bugs

---

## Architectural Standard (from copilot-instructions.md)

> **Controller Shape — Mandatory**
>
> Every controller action must follow this structure only:
>
> 1. Resolve current user via `RequireCurrentAuthorAsync()` or equivalent
> 2. Validate input — return early on failure
> 3. Call an application service
> 4. Map result to TempData or ViewModel
> 5. Return response
>
> Controllers must never contain: loops over domain entities, repository calls, domain mutations, branching business rules, or multi-step orchestration.

---

## Critical Violations

### 1. **AuthorController.Sections (lines 289-356)**
**Severity:** 🔴 CRITICAL

**Violations:**
- Complex multi-entity query orchestration
- **Nested foreach loops** over domain entities (lines 298-349)
- Business logic: publishability determination (line 300)
- Business logic: change classification computation (lines 304-349)
- HTML diff computation in controller (lines 333-335)
- Complex exception swallowing (lines 345-348)
- Data structure transformation (HashSet, Dictionary building)

**What it does:**
```csharp
// Lines 297-349: Complex orchestration that belongs in Application layer
var publishable = new HashSet<Guid>();
foreach (var (s, _) in sorted.Where(x => x.Section.NodeType == NodeType.Folder))
{
	if (await publicationService.CanPublishAsync(s.Id))
		publishable.Add(s.Id);
}

var classificationMap = new Dictionary<Guid, ChangeClassification>();
var chapterHasChanges = new HashSet<Guid>();
foreach (var (chapter, _) in sorted.Where(x => ...))
{
	try
	{
		var documents = sorted.Where(...).Select(...).ToList();
		if (!documents.Any(d => d.ContentChangedSincePublish))
			continue;

		chapterHasChanges.Add(chapter.Id);
		var highestClassification = ChangeClassification.Polish;
		var hasClassifiableVersion = false;

		foreach (var document in documents)
		{
			var latestVersion = await sectionVersionRepo.GetLatestAsync(document.Id);
			if (latestVersion is null) continue;

			hasClassifiableVersion = true;
			var diff = htmlDiffService.Compute(...);
			var classification = changeClassificationService.Classify(diff);
			if (classification.HasValue && classification.Value > highestClassification)
				highestClassification = classification.Value;
		}

		if (hasClassifiableVersion)
			classificationMap[chapter.Id] = highestClassification;
	}
	catch { /* silent swallow */ }
}
```

**Should be:** A single call to `ISectionManagementService.GetSectionsSummaryAsync(projectId)` returning a complete DTO with all computed metadata.

---

### 2. **AuthorController.Sync (lines 100-144)**
**Severity:** 🔴 CRITICAL

**Violations:**
- Direct domain entity mutation: `project.MarkSyncing()` (line 107)
- Direct repository call followed by UoW save (lines 108)
- **Inline `Task.Run` orchestration** (lines 111-141) — violates "Standardise sync kickoff" refactoring principle
- Multi-step error handling workflow (lines 126-139)
- Business logic: sync status determination and error message truncation (line 132)

**What it does:**
```csharp
// Lines 107-108: Direct domain mutation + UoW save
project.MarkSyncing();
await GetUnitOfWork().SaveChangesAsync();

// Lines 111-141: Complex background orchestration in controller
_ = Task.Run(async () =>
{
	using var scope = scopeFactory.CreateScope();
	var bgSyncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
	var bgProjectRepo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
	var bgUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
	try
	{
		await bgSyncService.ParseProjectAsync(projectId);
		logger.LogInformation(...);
	}
	catch (Exception ex)
	{
		logger.LogError(...);
		try
		{
			var failedProject = await bgProjectRepo.GetByIdAsync(projectId);
			if (failedProject is not null && failedProject.SyncStatus == SyncStatus.Syncing)
			{
				failedProject.UpdateSyncStatus(SyncStatus.Error, DateTime.UtcNow,
					ex.Message.Length > 200 ? ex.Message[..200] : ex.Message);
				await bgUnitOfWork.SaveChangesAsync();
			}
		}
		catch (Exception innerEx) { /* ... */ }
	}
});
```

**Should be:** `await syncOrchestrationService.StartSyncAsync(projectId, author.Id)`

---

### 3. **AuthorController.ActivateProject / DeactivateProject (lines 168-198)**
**Severity:** 🔴 CRITICAL

**Violations:**
- Direct repository queries (lines 172, 175)
- Domain entity mutation in controller: `DeactivateForReaders()`, `ActivateForReaders()` (lines 177, 179)
- Business rule: "only one active project" enforced in controller (lines 175-177)
- Direct UoW save (line 180)

**What it does:**
```csharp
// Lines 172-180: Business logic orchestration in controller
var project = await projectRepo.GetByIdAsync(projectId);
if (project is null) return NotFound();

var currentlyActiveProject = await projectRepo.GetReaderActiveProjectAsync();
if (currentlyActiveProject is not null && currentlyActiveProject.Id != project.Id)
	currentlyActiveProject.DeactivateForReaders();

project.ActivateForReaders();
await GetUnitOfWork().SaveChangesAsync();
```

**Should be:** `await projectManagementService.SetActiveProjectAsync(projectId, author.Id)`

---

### 4. **AuthorController.DeleteVersion (lines 620-646)**
**Severity:** 🔴 CRITICAL

**Violations:**
- Multi-step orchestration with branching business logic (lines 627-633)
- Repository query + LINQ ordering to determine "latest" (lines 627-628)
- **Business rule in controller:** "current version cannot be deleted" (line 631)
- Direct repository delete operation (line 635)
- Direct UoW save (line 636)

**What it does:**
```csharp
// Lines 627-636: Business logic + orchestration in controller
var allVersions = await sectionVersionRepo.GetAllBySectionIdAsync(sectionId);
var latest = allVersions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
if (latest is not null && latest.Id == versionId)
{
	TempData["Error"] = "The current version cannot be deleted. Use Revoke instead.";
	return Redirect(...);
}

await sectionVersionRepo.DeleteAsync(versionId);
await GetUnitOfWork().SaveChangesAsync();
```

**Should be:** `await versioningService.DeleteVersionAsync(versionId, author.Id)`

---

### 5. **AuthorController.Readers (lines 730-758)**
**Severity:** 🔴 CRITICAL

**Violations:**
- **Foreach loop** over domain entities (lines 735-755)
- Nested async repository calls inside loop (line 737)
- Business logic: reader status determination (lines 740-744)
- Complex conditional mapping logic (lines 746-754)

**What it does:**
```csharp
// Lines 735-755: Complex orchestration in controller
foreach (var r in readers.Where(r => !r.IsSoftDeleted))
{
	var pending = await invitationRepo.GetPendingByUserIdAsync(r.Id);
	var hasPending = pending.Count > 0;

	var status = r.IsActive
		? ReaderStatus.Active
		: hasPending
			? ReaderStatus.Invited
			: ReaderStatus.Inactive;

	rows.Add(new ReaderRowViewModel { /* ... */ });
}
```

**Should be:** `await readerManagementService.GetReadersSummaryAsync(author.Id)`

---

### 6. **AuthorController.AddProjects (lines 985-1077)**
**Severity:** 🔴 CRITICAL

**Violations:**
- **Massive multi-step orchestration** (93 lines!)
- **Three nested foreach loops** (lines 1002-1020, 1025-1036, 1037-1070)
- Direct domain entity creation: `Project.Create(...)` (line 1014)
- Direct repository operations (lines 1006, 1015, 1022, 1027, 1030, 1035)
- Business logic: soft-delete restore vs. new creation (lines 1007-1017)
- Duplicate `Task.Run` background sync orchestration (lines 1039-1069)
- Complex exception handling (lines 1019)

**What it does:** 93 lines of orchestration code — definitely belongs in Application layer.

**Should be:** `await projectManagementService.AddDiscoveredProjectsAsync(selectedUuids, author.Id)`

---

### 7. **AuthorController.Section (lines 865-899)**
**Severity:** 🟡 MODERATE

**Violations:**
- **Foreach loop** to build name map (lines 885-889)
- Multi-step query orchestration (lines 870-882)

**Should be:** `await sectionManagementService.GetSectionDetailAsync(sectionId, author.Id)`

---

### 8. **AuthorController.ResendInvitation (lines 820-848)**
**Severity:** 🟡 MODERATE

**Violations:**
- Business logic: conditional deactivation (lines 830-831)
- Multi-step orchestration (lines 825-839)
- Nested repository calls (lines 825, 833)

**Should be:** `await userService.ResendInvitationAsync(userId, author.Id)`

---

### 9. **BaseReaderController.BuildCommentDisplayModelsAsync (lines 263-355)**
**Severity:** 🔴 CRITICAL

**Violations:**
- **Massive helper method** (93 lines) that's actually a domain service
- **Multiple foreach loops** over collections (lines 281-285, 294-304, 318-325, 327-354)
- Complex data structure transformations (GroupBy, Dictionary building)
- Business logic: comment visibility filtering (lines 269-271)
- Business logic: permission determination (lines 331, 339, 342-343)
- Nested async repository calls in loops (lines 283, 298, 323)

**What it does:**
```csharp
// Lines 263-355: This is a full application service disguised as a helper
protected async Task<IReadOnlyList<CommentDisplayViewModel>> BuildCommentDisplayModelsAsync(...)
{
	// Filters comments
	var visibleComments = comments.Where(c => !c.IsSoftDeleted).ToList();

	// Groups by parent
	var commentsByParentId = visibleComments.Where(...).GroupBy(...).ToDictionary(...);

	// Fetches author names
	foreach (var authorId in authorIds)
	{
		var author = await userRepository.GetByIdAsync(authorId);
		authorNames[authorId] = author?.DisplayName ?? "Unknown";
	}

	// Fetches passage anchors
	foreach (var anchorId in anchorIds)
	{
		anchorsById[anchorId] = await PassageAnchorService.GetByIdAsync(...);
	}

	// Fetches audit user names
	foreach (var auditUserId in auditUserIds)
	{
		var auditUser = await userRepository.GetByIdAsync(auditUserId);
		authorNames[auditUserId] = auditUser?.DisplayName ?? "Unknown";
	}

	// Maps to view models with business logic
	return visibleComments.Select(comment => { /* complex mapping */ }).ToList();
}
```

**Should be:** `await commentService.GetCommentDisplayDataAsync(commentIds, currentUserId, projectAuthorId)`

---

### 10. **BaseReaderController Helper Methods (lines 357-442)**
**Severity:** 🟡 MODERATE

**Violations:**
- `BuildContentGroups` (lines 385-420): **Recursive tree-building algorithm** with business rules
- `HasPublishedChapter` (lines 436-442): **Recursive query** with business logic
- `GetTopLevelAncestor` (lines 357-368): Domain navigation logic
- `BuildBreadcrumb` (lines 370-383): Data transformation logic

**What they do:** Complex data structure transformations and recursive tree navigation — all Application layer concerns.

**Should be:** These should be in `IContentNavigationService` or similar.

---

### 11. **BaseReaderController.AddComment (lines 46-109)**
**Severity:** 🟡 MODERATE

**Violations:**
- Multi-step orchestration with complex conditional logic (lines 73-108)
- Business logic: determining redirect anchor based on node type (lines 78-88)
- Business logic: mobile vs. desktop routing decisions (lines 90-108)

**Should be simplified:** The service should return a redirect hint, not force controller to determine routing.

---

### 12. **BaseReaderController.IsMobile (lines 422-434)**
**Severity:** 🟢 LOW (but questionable)

**Violation:** User-Agent parsing logic in controller. This is HTTP plumbing, not business logic, but feels like it should be in a separate service.

**Consider:** `IMobileDetectionService.IsMobileRequest(HttpContext)`

---

## Summary by Controller

| Controller | Critical Violations | Moderate Violations | Total Lines of Business Logic |
|------------|---------------------|---------------------|-------------------------------|
| **AuthorController** | 7 | 2 | **~350 lines** |
| **BaseReaderController** | 2 | 3 | **~180 lines** |
| **Other controllers** | Not yet reviewed | Not yet reviewed | TBD |

---

## Recommended Refactoring Priority

### Phase 1: Extract Most Critical Orchestration
1. **AuthorController.Sections** → `ISectionManagementService.GetSectionsSummaryAsync`
2. **AuthorController.AddProjects** → `IProjectManagementService.AddDiscoveredProjectsAsync`
3. **BaseReaderController.BuildCommentDisplayModelsAsync** → `ICommentDisplayService.GetCommentDisplayDataAsync`

### Phase 2: Standardise Background Sync
4. **AuthorController.Sync** → `ISyncOrchestrationService.StartSyncAsync`
5. **AuthorController.AddProjects (Task.Run block)** → Use same service

### Phase 3: Extract Domain Mutations
6. **AuthorController.ActivateProject/DeactivateProject** → `IProjectManagementService.SetActiveProjectAsync`
7. **AuthorController.DeleteVersion** → `IVersioningService.DeleteVersionAsync`
8. **AuthorController.RemoveProject** → `IProjectManagementService.SoftDeleteProjectAsync`

### Phase 4: Extract Helper Orchestration
9. **AuthorController.Readers** → `IReaderManagementService.GetReadersSummaryAsync`
10. **AuthorController.Section** → `ISectionManagementService.GetSectionDetailAsync`
11. **BaseReaderController tree-building helpers** → `IContentNavigationService`

---

## Impact Assessment

### Testability
❌ **Current:** Controllers contain untested business logic  
✅ **After refactoring:** All business logic testable via Application service tests

### Maintainability
❌ **Current:** Business rules scattered across controllers  
✅ **After refactoring:** Single source of truth in Application layer

### Architectural Drift
❌ **Current:** Layer boundaries violated throughout Web layer  
✅ **After refactoring:** Clean layered architecture enforced

---

## Notes

- **DatabaseSeeder** also contains orchestration logic but is a special case (data seeding tool)
- **AccountController** not yet reviewed — may contain email handling logic
- **DropboxController** not yet reviewed
- **ReaderController**, **DiscoveryController**, **SupportController** not yet reviewed

---

## Next Steps

1. **Immediate:** Add this review to TASKS.md as a new refactoring phase
2. **Plan:** Schedule Phase 1 refactoring after current sprint work completes
3. **Standard:** Add pre-merge review checklist: "Does this controller contain loops, entity mutations, or multi-step orchestration?"
4. **Tooling:** Consider adding a Roslyn analyzer to detect foreach/while loops in controller methods

---

**Conclusion:** The Web layer requires significant refactoring to align with documented architectural standards. This is a substantial body of work but critical for long-term maintainability.
