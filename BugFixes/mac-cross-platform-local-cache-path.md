You are working in the DraftView repository.

Goal:
Register and implement a bug fix for cross-platform local cache path resolution, using a Mac-owned long-running bugfix branch strategy.

Branching Strategy (strictly follow):
- The Mac owns a long-running branch: BugFix-Mac
- All bug fixes must branch from BugFix-Mac, not main
- Each individual bug fix is developed in its own branch from BugFix-Mac
- After completion, the bugfix branch is merged back into BugFix-Mac
- BugFix-Mac will later be merged into main as a batch (not part of this task)

Work in this exact order. Do not skip steps. Do not guess file contents. Inspect files before changing them.

------------------------------------------------------------
1. Ensure BugFix-Mac branch exists and is up to date
------------------------------------------------------------
- checkout main
- pull latest main
- create or update branch BugFix-Mac from main
- switch to BugFix-Mac

------------------------------------------------------------
2. Create a bugfix branch from BugFix-Mac
------------------------------------------------------------
Create a new branch:

bugfix/mac-cross-platform-local-cache-path

This branch will contain only this bug fix.

------------------------------------------------------------
3. Update TASKS.md
------------------------------------------------------------
Add a new Bug Fix task entry.

Title:
Cross-platform local cache path resolution and automatic cache directory creation

Type:
Bug Fix

Problem:
The application uses a Windows-specific LocalCachePath in shared configuration, which breaks macOS and Ubuntu environments and requires manual folder creation.

Root Cause:
- Machine-specific path stored in shared appsettings
- No platform-aware fallback
- Cache directory not guaranteed to exist

Required Outcome:
- Shared config must not contain OS-specific paths
- Cache path must resolve automatically on Windows, macOS, and Ubuntu
- If configured, use DraftView:LocalCachePath
- If not configured, resolve platform defaults
- Ensure directory exists before use
- Keep filesystem logic in Infrastructure layer
- Do not introduce OS-specific config files

Acceptance Criteria:
- Works on Windows, macOS, Ubuntu without manual setup
- Cache directory created automatically
- No hardcoded Windows path in repo
- Logs show resolved cache root
- Dropbox download works on fresh machine

------------------------------------------------------------
4. Inspect relevant files BEFORE changes
------------------------------------------------------------
Read contents of:
- TASKS.md
- DraftView.Web/appsettings.json
- any Development appsettings file
- ILocalPathResolver implementation
- settings/options classes for DraftView
- DropboxFileDownloader.cs

Do not assume structure. Inspect first.

------------------------------------------------------------
5. Implement the fix (architecture-first)
------------------------------------------------------------
Apply these rules:

Configuration:
- Remove Windows-specific LocalCachePath from shared appsettings
- Replace with empty or safe default

Path resolution:
- If LocalCachePath is configured and non-empty → use it
- Otherwise resolve:
  - Windows: LocalApplicationData/DraftView/Cache
  - macOS: ~/Library/Application Support/DraftView/Cache
  - Linux: ~/.local/share/DraftView/Cache

Implementation:
- Use Path.Combine and platform-safe APIs
- Ensure directory exists with Directory.CreateDirectory
- Resolve and log cache root once
- Fail clearly if configured path is invalid

Architecture:
- Keep logic inside Infrastructure (ILocalPathResolver or equivalent)
- Do not push filesystem logic into controllers or application layer
- Do not introduce OS-specific config files

------------------------------------------------------------
6. Tests (required before finalising)
------------------------------------------------------------
- Add tests covering:
  - configured path is respected
  - fallback works for each platform
  - directory creation occurs
- Tests must be deterministic (do not rely on actual OS where possible)
- Introduce abstraction seam if needed for OS detection

------------------------------------------------------------
7. Validate
------------------------------------------------------------
- Run restore, build, and tests
- Ensure no Windows-specific paths remain in shared config
- Confirm directory creation logic is correct
- Confirm no regression in Dropbox download flow

------------------------------------------------------------
8. Commit and merge into BugFix-Mac
------------------------------------------------------------
Commit in logical steps:
- TASKS.md update
- tests
- implementation

Then:
- merge bugfix/mac-cross-platform-local-cache-path into BugFix-Mac
- remain on BugFix-Mac branch

------------------------------------------------------------
9. Final report
------------------------------------------------------------
Report:
- files changed
- root cause
- before vs after behaviour
- test results
- any follow-up risks

------------------------------------------------------------
Constraints
------------------------------------------------------------
- Do not refactor unrelated code
- Do not modify behaviour outside this bug
- Do not stop after TASKS.md
- Complete full workflow end-to-end
- Do not ask for confirmation mid-task