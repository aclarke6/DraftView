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
No active task file. Consult `Docs/TASKS.md` for the current state of all work tracks
and choose the highest-priority open item.

## How to work
1. Check open "Claude CLI" issues first: `gh issue list --label "Claude CLI" --state open`
2. Read `Docs/TASKS.md` for priority context before starting any task
3. All bugs and sprint work runs from a GitHub Issue — read the issue for full detail
4. For implementation tasks, follow the red/green cycle strictly:
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

## Important facts

### CSS version bump
`DraftView.Web/wwwroot/css/DraftView.Core.css` contains a line like:
```css
--css-version: "v2026-08-21-3";
```
When modifying any CSS file, increment the version. Use regex replace — never
hardcode the expected current value. Pattern:
```powershell
$content = $content -replace 'v\d{4}-\d{2}-\d{2}-\d+', 'vNEW_VERSION'
```
Then verify the replacement applied before saving. Update all standalone views
(`Layout = null`) as well as `_Layout.cshtml` and `DraftView.Core.css`.

### EF migration commands
Run from the solution root:
```
dotnet ef migrations add <MigrationName> --project DraftView.Infrastructure --startup-project DraftView.Web
dotnet ef database update --project DraftView.Infrastructure --startup-project DraftView.Web
```

### Infrastructure test database
Use `UseInMemoryDatabase` (not SQLite) — see `ScrivenerProjectRepositoryTests`
for the exact pattern. Each test class gets a fresh `Guid.NewGuid().ToString()`
database name in the constructor.

### Document location
All project documents (design docs, sprint plans, task files, reference SQL, etc.)
are in the `Docs/` folder. Key files:
- `Docs/TASKS.md` — active task list and sprint status
- `AGENTS.md` — authoritative execution rules for all coding agents (root, alongside CLAUDE.md)
- `Docs/HISTORY.md` — completed work log
- `Docs/MultiTenancy.md` — multi-tenancy design and sprint plan
