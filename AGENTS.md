# Repository Guidelines

## Project Structure & Module Organization
`DraftView.slnx` ties together a layered .NET solution targeting `net10.0`. Core domain types live in `DraftView.Domain`, use-case and orchestration logic in `DraftView.Application`, integrations and persistence in `DraftView.Infrastructure`, and the ASP.NET Core MVC site in `DraftView.Web`. Tests mirror those layers in `DraftView.Domain.Tests`, `DraftView.Application.Tests`, `DraftView.Infrastructure.Tests`, `DraftView.Web.Tests`, and `DraftView.Integration.Tests`. Static assets and Razor views live under `DraftView.Web/wwwroot` and `DraftView.Web/Views`.

## Build, Test, and Development Commands
Use the solution root for CLI work.

- `dotnet restore DraftView.slnx`: restore NuGet packages for all projects.
- `dotnet build DraftView.slnx`: compile the full solution.
- `dotnet test DraftView.slnx`: run the complete xUnit test suite.
- `dotnet run --project DraftView.Web`: start the web app locally.
- `dotnet test DraftView.Application.Tests` or `dotnet test DraftView.Web.Tests`: run one test project while iterating.

The web app applies EF Core migrations and seeds default data on startup, so confirm your local PostgreSQL connection before running it.

## Coding Style & Naming Conventions
Follow the existing C# style: 4-space indentation, file-scoped namespaces, `PascalCase` for types and public members, `camelCase` for locals and parameters, and one class per file where practical. Keep service and repository names explicit, for example `ReadingProgressService` or `ScrivenerProjectRepository`. Prefer small, focused methods and keep layer boundaries intact: web code should not bypass application/domain abstractions.

## Testing Guidelines
Tests use xUnit with Moq. Name test files after the subject under test, such as `UserServiceTests.cs`, and prefer descriptive method names in the `Method_Scenario_ExpectedResult` style already used across the repo. Add or update tests in the matching `*.Tests` project whenever domain rules, parsing, persistence, or controller behavior changes.

## Commit & Pull Request Guidelines
Recent commits use short, imperative summaries, often scoped to a feature or script update, for example `Add prose font preferences application contract, tests, and implementation`. Keep commits focused and explain the behavioral change, not just the file touched. PRs should include a clear summary, linked issue or sprint item when applicable, test evidence (`dotnet test ...`), and screenshots for UI changes.

## Security & Configuration Tips
Configuration lives in `DraftView.Web/appsettings.json` plus user secrets (`UserSecretsId` is enabled in the web project). Do not commit real passwords, API tokens, or machine-specific overrides. Keep local connection strings, seed credentials, and SMTP secrets in user secrets or developer-local settings.

## AI Agent Operating Rules

Architecture:
- Strictly follow layered architecture: Domain, Application, Infrastructure, Web
- No cross-layer access or shortcuts
- Do not bypass services or repositories

Change Control:
- Do not modify code immediately
- Always explain the proposed change first
- Wait for explicit approval before applying changes
- Limit changes to the smallest possible scope

Development Process:
- Follow architecture-first design
- Reuse existing code before creating new structures
- Extract helper methods instead of duplicating logic
- Do not introduce new dependencies without approval

Debugging:
- Reproduce and isolate faults before making changes
- Prefer analysis and explanation over immediate fixes
- Do not guess at solutions without identifying root cause

Testing:
- Check for existing tests before adding new ones
- Add tests when behaviour changes
- Do not delete or bypass tests without explanation

Safety:
- Do not run destructive commands
- Do not execute git operations without approval
- Do not perform large refactors unless explicitly requested

General:
- If unclear, ask instead of assuming
- Do not invent files, services, or behaviours
- Keep responses concise and focused