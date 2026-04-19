---
mode: agent
description: BUG-004 — ForgotPassword returns HTTP 405 and no custom error page
---

# BUG-004 — ForgotPassword returns HTTP 405 with no custom error page

## Branching
Branch from `BugFix-Mac` (or `BugFix-PC` if working on Windows):
`bugfix/BUG-004-forgotpassword-405`

## Problem
Navigating to `/Account/ForgotPassword` returns HTTP 405 Method Not Allowed.
The raw browser 405 page is shown — there is no custom 405 error page.

## Observed Behaviour
- User navigates to `/Account/ForgotPassword`
- Browser shows raw HTTP ERROR 405 page
- No custom DraftView error page shown

## Expected Behaviour
- `/Account/ForgotPassword` GET should show the forgot password form
- `/Account/ForgotPassword` POST should process the submission
- Any 405 error should be handled by the custom error middleware and show a
  friendly error page, not the raw browser error

## Two Issues to Fix

### Issue 1 — Route/method mismatch on ForgotPassword
Before writing any code, read:
1. `DraftView.Web/Controllers/AccountController.cs` — `ForgotPassword` GET and POST actions
2. Check `[HttpGet]` and `[HttpPost]` attributes are correctly applied
3. Check the form `method="post"` in `Views/Account/ForgotPassword.cshtml`
4. Check route configuration in `Program.cs`

Confirm whether the 405 is caused by:
- Missing `[HttpGet]` on the GET action
- Missing `[HttpPost]` on the POST action
- A routing conflict
- Antiforgery token misconfiguration

Note: deploy the latest build first and retest — this may already be fixed.

### Issue 2 — No custom 405 error page
Read:
1. `DraftView.Web/Program.cs` — how error handling middleware is configured
2. `DraftView.Web/Controllers/HomeController.cs` — `Error` action
3. `DraftView.Web/Views/Shared/Error.cshtml` — existing error view

The `UseStatusCodePagesWithReExecute` middleware should handle 405 the same as
404. Check if it is configured and covers 405.

## Rules
- Deploy latest build and retest before fixing — may already be resolved
- Reproduce and confirm root cause before changing any production code
- TDD where applicable
- Fix issue 1 and issue 2 as separate commits

## Commits
- `bugfix: BUG-004a — fix ForgotPassword route/method mismatch`
- `bugfix: BUG-004b — add 405 handling to error middleware`
- Merge back to `BugFix-Mac` or `BugFix-PC` when complete
