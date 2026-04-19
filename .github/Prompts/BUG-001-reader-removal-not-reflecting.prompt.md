---
mode: agent
description: BUG-001 — Removing a reader does not remove them from the list
---

# BUG-001 — Removing a reader does not remove them from the Readers list

## Branching
Branch from `BugFix-Mac` (or `BugFix-PC` if working on Windows):
`bugfix/BUG-001-reader-removal-not-reflecting`

## Problem
Submitting the Remove action on `/Author/Readers` completes without error but the
reader remains visible in the list on the next page load.

## Observed Behaviour
- Author clicks Remove on a reader row
- Action completes (no error shown)
- Page reloads — reader still appears in the list

## Expected Behaviour
- Reader is removed from the visible list after the action completes
- Or a clear message explains why the removal was not applied

## Investigation
Before writing any code, read and inspect:
1. `DraftView.Web/Controllers/AuthorController.cs` — `SoftDeleteReader` action
2. `DraftView.Application/Services/UserService.cs` — `SoftDeleteUserAsync`
3. `DraftView.Web/Views/Author/Readers.cshtml` — how the list is filtered
4. `DraftView.Domain/Entities/User.cs` — `IsSoftDeleted` flag

Confirm:
- Does `SoftDeleteUserAsync` actually set `IsSoftDeleted = true` and save?
- Does the Readers view filter on `IsSoftDeleted`?
- Is `SaveChangesAsync` called after the soft delete?
- Does `SoftDeleteReader` also revoke `ReaderAccess` records?

## Rules
- Reproduce and confirm root cause before changing any production code
- TDD: write a failing test that proves the bug before fixing it
- No production code changes until root cause is confirmed
- Follow architecture rules: Web layer handles HTTP, Application layer owns logic

## Commit
- `bugfix: BUG-001 — fix reader removal not reflecting in Readers list`
- Merge back to `BugFix-Mac` or `BugFix-PC` when complete
