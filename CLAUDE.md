# DraftView — Claude Code Instructions

## Project
ASP.NET Core MVC, .NET 10, PostgreSQL, EF Core, xUnit + Moq.
Solution: `DraftView.slnx` at `C:\Users\alast\source\repos\DraftView`

## Non-negotiable rules
- TDD: failing test before every implementation. No exceptions.
- Never use `&&` in PowerShell — use `;`
- Run `dotnet test` after every red/green cycle and report the test count
- Never hardcode a CSS version string — always use regex replace
- Every new author-scoped entity carries `AuthorId`
- `param()` must be the absolute first line of any `.ps1` file
- Full CSS rule blocks only — never partial `{}` fragments

## Current task
No active task file. Consult `TASKS.md` for the current state of all work tracks
and choose the highest-priority open item. `CLAUDE_TASK_Notifications.md` is
complete — all seven phases are implemented and merged to main.

## How to work
1. Read `TASKS.md` fully before starting any task
2. For implementation tasks, follow the red/green cycle strictly:
   - Write the stub (NotImplementedException) first
   - Write the failing tests
   - Confirm RED with `dotnet test --filter <TestClass>`
   - Implement
   - Confirm GREEN with `dotnet test --filter <TestClass>`
   - Run full `dotnet test` and report count before moving to next phase
3. Do not proceed to the next phase until all tests in the current phase are GREEN
4. Do not modify any file outside the scope described in the task

## Key files (read these before starting)

### Existing patterns to follow
- Entity pattern: `DraftView.Domain/Entities/Comment.cs`
- InvariantViolationException usage: `DraftView.Domain/Exceptions/InvariantViolationException.cs`
- Repository pattern: `DraftView.Infrastructure/Persistence/Repositories/SystemStateMessageRepository.cs`
- EF configuration pattern: `DraftView.Infrastructure/Persistence/Configurations/SystemStateMessageConfiguration.cs`
- Infrastructure test pattern: `DraftView.Infrastructure.Tests/Persistence/ScrivenerProjectRepositoryTests.cs`
- Application service test pattern: `DraftView.Application.Tests/Services/DashboardServiceTests.cs`
- DI registration: `DraftView.Web/Extensions/ServiceCollectionExtensions.cs`

### Files this task modifies
- NEW: `DraftView.Domain/Entities/AuthorNotification.cs`
- NEW: `DraftView.Domain/Interfaces/Repositories/IAuthorNotificationRepository.cs`
- NEW: `DraftView.Infrastructure/Persistence/Repositories/AuthorNotificationRepository.cs`
- NEW: `DraftView.Infrastructure/Persistence/Configurations/AuthorNotificationConfiguration.cs`
- NEW: `DraftView.Domain.Tests/Entities/AuthorNotificationTests.cs`
- NEW: `DraftView.Infrastructure.Tests/Persistence/AuthorNotificationRepositoryTests.cs`
- MODIFY: `DraftView.Infrastructure/Persistence/DraftViewDbContext.cs` — add DbSet
- MODIFY: `DraftView.Domain/Interfaces/Services/IDashboardService.cs` — replace notification method
- MODIFY: `DraftView.Application/Services/DashboardService.cs` — replace implementation
- MODIFY: `DraftView.Application/Services/CommentService.cs` — write notification on comment
- MODIFY: `DraftView.Application/Services/UserService.cs` — write notification on invite accept
- MODIFY: `DraftView.Application/Services/SyncService.cs` — write notification on sync complete
- MODIFY: `DraftView.Application.Tests/Services/DashboardServiceTests.cs` — update and extend
- MODIFY: `DraftView.Application.Tests/Services/CommentServiceTests.cs` — add notification tests
- MODIFY: `DraftView.Application.Tests/Services/UserServiceInvitationAcceptanceTests.cs` — add notification test
- MODIFY: `DraftView.Application.Tests/Services/SyncServiceTests.cs` — add notification tests
- MODIFY: `DraftView.Web/Extensions/ServiceCollectionExtensions.cs` — register new repo
- MODIFY: `DraftView.Web/Models/AuthorViewModels.cs` — update DashboardViewModel
- MODIFY: `DraftView.Web/Controllers/AuthorController.cs` — add Dismiss/ClearAll actions
- MODIFY: `DraftView.Web/Views/Author/Dashboard.cshtml` — dismiss button, clear all, viewport fix
- MODIFY: `DraftView.Web/wwwroot/css/DraftView.Notifications.css` — dismiss button styles + viewport fix

## Important facts

### NotificationEventType enum
Already exists in `DraftView.Domain/Notifications/NotificationItemDto.cs`.
Do NOT create a new enum — reuse the existing one.
In Phase 5, if `NotificationItemDto` is fully removed, move `NotificationEventType`
to its own file `DraftView.Domain/Notifications/NotificationEventType.cs` first,
then remove `NotificationItemDto.cs`.

### GetAuthorAsync
`IUserRepository.GetAuthorAsync(CancellationToken ct)` already exists and is
implemented. Use it in CommentService, UserService, and SyncService to resolve
the author's `Id` for writing notifications.

### Infrastructure test database
Use `UseInMemoryDatabase` (not SQLite) — see `ScrivenerProjectRepositoryTests`
for the exact pattern. Each test class gets a fresh `Guid.NewGuid().ToString()`
database name in the constructor.

### DashboardService constructor change
Current constructor parameters:
```
ISectionRepository, IUserRepository, IEmailDeliveryLogRepository,
ICommentRepository, IInvitationRepository, IScrivenerProjectRepository
```
After Phase 3, becomes:
```
ISectionRepository, IUserRepository, IEmailDeliveryLogRepository,
IAuthorNotificationRepository, IUnitOfWork
```
`ICommentRepository`, `IInvitationRepository`, `IScrivenerProjectRepository`
are removed from DashboardService (they may still be used elsewhere — do not
remove their registrations from DI).

### CSS version bump
`DraftView.Web/wwwroot/css/DraftView.Core.css` contains a line like:
```css
--css-version: "v2026-04-10-1";
```
When modifying any CSS file, increment the version. Use regex replace — never
hardcode the expected current value. Pattern:
```powershell
$core = $core -replace '--css-version: "v[^"]+"', '--css-version: "vNEW_VERSION"'
```
Then verify the replacement applied before saving.

### EF migration commands
Run from the solution root:
```
dotnet ef migrations add AddAuthorNotifications --project DraftView.Infrastructure --startup-project DraftView.Web
dotnet ef database update --project DraftView.Infrastructure --startup-project DraftView.Web
```

### Final commit
After all phases complete and all tests are GREEN:
```
dotnet test
git add -A
git commit -m "Persisted AuthorNotification entity, dismiss/clear all UI, viewport panel fix"
git push
.\publish-draftview.ps1
```
