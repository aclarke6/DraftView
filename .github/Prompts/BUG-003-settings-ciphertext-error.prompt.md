---
mode: agent
description: BUG-003 — Reader settings shows Ciphertext decryption error on screen
---

# BUG-003 — Reader settings shows `Ciphertext is not in the expected format`

## Branching
Branch from `BugFix-Mac` (or `BugFix-PC` if working on Windows):
`bugfix/BUG-003-settings-ciphertext-error`

## Problem
When a reader changes their display name or password in Account Settings, the page
shows `Ciphertext is not in the expected format` as a form validation error instead
of routing through the controlled 500 error path.

## Observed Behaviour
- Reader submits the settings form
- Page returns with error: `Ciphertext is not in the expected format`
- Displayed as a form validation message (user-facing)

## Expected Behaviour
- Protected-email decryption failures are operational failures
- They must be logged and routed through the controlled 500 error path
- They must never appear as user-facing form validation messages

## Investigation
Before writing any code, read and inspect:
1. `DraftView.Web/Controllers/AccountController.cs` — settings POST actions
2. `DraftView.Application/Services/IUserEmailAccessService.cs` — controlled email access
3. `DraftView.Infrastructure/Persistence/Repositories/UserRepository.cs` — email rehydration
4. Production database — check for rows with invalid `EmailCiphertext` values

Confirm:
- Which action triggers the decryption attempt?
- Is the exception being caught and added to `ModelState` somewhere?
- Is the issue caused by invalid ciphertext in the database, or a code path error?
- Are any rows in `AppUsers` missing or having malformed `EmailCiphertext`?

Check for bad rows on production:
```bash
/tmp/run-query.sh -c "SELECT COUNT(*) FROM \"AspNetUsers\" WHERE \"EmailCiphertext\" IS NULL OR LENGTH(\"EmailCiphertext\") < 10;"
```

## Rules
- Reproduce and confirm root cause before changing any production code
- TDD: write a failing test before fixing
- Operational failures must route to controlled error path — never leak as form errors
- Do not fix bad database rows as a workaround — fix the code path first

## Commit
- `bugfix: BUG-003 — route ciphertext decryption failures to controlled error path`
- Merge back to `BugFix-Mac` or `BugFix-PC` when complete
