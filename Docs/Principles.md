# DraftView — Session Coding Principles

## PowerShell Output
- Every command that produces output must pipe through `2>&1 | Tee-Object -Variable b; $b | clip`
- No exceptions — this shows output in console AND copies to clipboard simultaneously
- Short commands (under ~50 lines) pasted directly into terminal
- Scripts 50+ lines delivered as `.ps1` files using `present_files`

## Script Verification — MANDATORY
- Every string replacement must detect line endings first:
  powershell
  $le = if ($content -match "`r`n") { "`r`n" } else { "`n" }
  
- Use `$le` in all match strings — never assume CRLF or LF
- Compare old and new content — if `$newContent -eq $content` → `Write-Host "ERROR"` and `exit 1`
- Never build after a replacement without first verifying it applied
- Never assume a previous step succeeded — verify explicitly

## CSS Changes — MANDATORY
- Every script touching any `.css` file must bump `--css-version` in `DraftView.Core.css`
- Always use regex replace — never hardcode the expected current version:
  powershell
  $core = $core -replace '--css-version: "v[^"]+";', '--css-version: "v2026-04-02-1";'
  if ($core -notmatch 'v2026-04-02-1') { Write-Host "ERROR: bump failed" -ForegroundColor Red; exit 1 }
  
- Every script touching CSS `?v=` query strings must update ALL of the stylesheet css link calls
  in `_Layout.cshtml` to match the new `--css-version` value — never leave any on a stale version
- Verify the bump applied before saving

## Controller Guards — MANDATORY
- Every public action must use ASP.Net Core's `[Authorize]` attribute to guard against unauthorized access
  

## Build and Test
- Always build before running the app: `dotnet build DraftView.slnx --no-restore -v q`
- Always run tests after code changes: `dotnet test DraftView.slnx --no-build -v q`
- "Green tests" from the user means all passed — do not ask them to paste if they say this
- "Clean build" means 0 errors, 0 warnings — do not ask them to paste if they say this
- Do not ask for build or test confirmation unless there is a failure

## File Changes
- Never guess at file contents — always read before writing replacement scripts
- Full file rewrites preferred over regex patching for complex files
- PowerShell here-strings must use single-quoted `@'...'@` to avoid quote mangling
- Line endings: detect with `$le` before every replacement — never assume
- Scripts start with `cls` and end with the next required command
- Never send multiple separate inline blocks for a single logical change — combine into one properly structured script

## TDD
- Required for all Domain, Application, and Infrastructure changes — not just domain entities
- Tests first, implementation second — no exceptions
- Read existing test files before adding new tests to match style

## Architecture
- Tenancy-agnostic: every new table with author-scoped data gets `AuthorId`
- No full tenancy model until product is live with a single author and billing in place
- Repository methods returning scoped data must accept or imply `AuthorId`

## Script Naming
- Format: `Step{N}-{DayAbbrev}-{Description}.ps1`
- Example: `Step12-Thur-SyncFileProgress.ps1`
- Day abbreviation matches the session day
- N increments from the last step in the repo
- Every script starts with a header comment block listing all files changed:
  powershell
  # Step12-Thur-SyncFileProgress.ps1
  # Changes:
  #   - File1.cs: description
  #   - File2.cs: description
  

## General Conduct
- Never suggest the user hasn't run something, restarted the app, or rebuilt
- Never suggest "cache is stale" as a diagnosis
- Never ask "shall we get back to the task list?"
- Do not add `Write-Host` prompts or prose like "Then run:" before commands
- Scripts end with the next required command ready to run — no trailing explanation
- All responses follow the TASKS.md task order — new ideas go into TASKS.md, not into the current session
- Do not ask for confirmation of successful builds or green tests unless there is a failure

## Project Tree
- Use `Show-Project.ps1` for a full project tree of source files