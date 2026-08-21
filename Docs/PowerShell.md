# PowerShell Scripting Standards
# DraftView Project
# Last updated: 2026-03-31

---

## General Rules

- Always add `$ErrorActionPreference = "Stop"` at the top of every script
- Always use `Set-Content` with `-Encoding UTF8` for file writes
- Never use regex patching — always use `$content.Replace($old, $new)`
- Never use here-strings (`@' '@`) for `$old`/`$new` in patch scripts — line ending mismatches will cause silent failures
- Build multiline strings using string concatenation with `$le` for line endings
- Always deliver scripts 50+ lines as `.ps1` files, not pasted inline
- Short scripts (<50 lines) may be pasted directly into the terminal
- Always add `cls` at the top of pasted terminal scripts
- End scripts with the next required command (e.g. `dotnet build`) rather than a `Write-Host` prompt
- Never use markdown fences (`  `) around terminal commands — use plain text or `Write-Host`

---

## Line Endings

- Never hardcode `` `r`n `` or `` `n `` in patch strings
- Always detect line endings first using `Get-LineEndings.ps1`:

$le = if ((.\Get-LineEndings.ps1 -FilePath $file) -eq "CRLF") { "`r`n" } else { "`n" }


- Build all multiline `$old`/`$new` strings using `$le`:

$old = "first line" + $le + "second line"


---

## Patch Script Structure

Every patch script must follow this structure:

$ErrorActionPreference = "Stop"
$file = "C:\path\to\file"
$le = if ((.\Get-LineEndings.ps1 -FilePath $file) -eq "CRLF") { "`r`n" } else { "`n" }
$content = Get-Content $file -Raw

$old = "exact string to replace" (use $le for line breaks)
$new = "replacement string" (use $le for line breaks)

if (-not $content.Contains($old)) { Write-Error "PATCH FAILED: target not found."; exit 1 }
$content = $content.Replace($old, $new)
Set-Content $file $content -Encoding UTF8

if (-not ((Get-Content $file -Raw).Contains("unique string from new"))) { Write-Error "PATCH FAILED: verification failed."; exit 1 }
Write-Host "Patch applied OK."


---

## Validation

- Every patch must include a pre-check (`$content.Contains($old)`) and a post-check
- Pre-check: verify the target string exists before attempting replacement
- Post-check: verify a unique string from `$new` exists after writing
- Both checks must use `Write-Error` + `exit 1` on failure
- `$ErrorActionPreference = "Stop"` ensures `exit 1` actually halts execution

---

## Database Queries

- Always use `.\pg.ps1 -f "$env:TEMP\q.sql"` for PostgreSQL queries
- Write SQL to a temp file first:
  `[System.IO.File]::WriteAllText("$env:TEMP\q.sql", $sql, [System.Text.Encoding]::ASCII)`
- Always fully quote identifiers: `"TableName"`, `"ColumnName"`
- Set `$env:PAGER = ""` to suppress the MORE pager in psql output
- Credentials are stored in `dotnet user-secrets`

---

## Build

- Always use: `dotnet build DraftView.slnx --nologo | Tee-Object -Variable b; $b | clip`
- Solution file is `DraftView.slnx` — never use `.sln`
- Stop the running app before building to avoid DLL lock errors

---

## CSS Version Bumping

- Never use regex to patch the CSS version — the quoted value will be mangled
- Use `$content.Replace($old, $new)` with the exact current version string as `$old`
- Update both `DraftView.Core.css` (the `--css-version` variable) and `_Layout.cshtml` (all `?v=` query strings)
- Version format: `v{YYYY}-{MM}-{DD}-{seq}` e.g. `v2026-03-31-1`

---

## Reusable Scripts

- `Get-LineEndings.ps1` — returns CRLF, LF, MIXED, or UNKNOWN for a given file
- `pg.ps1` — runs a PostgreSQL query using credentials from dotnet user-secrets
- `patch-css-version.ps1` — DO NOT USE (broken, regex-based) — replace manually

---

## Git

- Commit after each completed task
- Commit message format: `TaskId: brief description`
- Example: `1B + 1C + 1D: invitation email expiry, readers list status, recipient name fix`
