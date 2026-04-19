---
mode: agent
description: BUG-005 — Password reset link immediately shows Link Expired
---

# BUG-005 — Password reset link immediately shows Link Expired

## Branching
Branch from `main`:
`bugfix/BUG-005-password-reset-link-expired`

## Symptoms
1. User navigates to `/Account/ForgotPassword`
2. Enters their email address and submits
3. Receives the password reset email with a valid-looking link
4. Clicks the link immediately — it has not had time to expire
5. Browser shows `/Account/ResetPasswordInvalid` — "This password reset link has expired or has already been used"

## Before Writing Anything
Read these files in full:
1. `DraftView.Web/Controllers/AccountController.cs` — `ForgotPassword` POST and `ResetPassword` GET and POST actions
2. `DraftView.Domain/Entities/PasswordResetToken.cs` — `IsValid()`
3. `DraftView.Web/Controllers/BaseController.cs` — `GetUserByEmailAsync`
4. `DraftView.Web.Tests/PasswordResetRegressionTests.cs` — understand the existing test setup fully

Trace the complete code path from `ForgotPassword` POST through to `ResetPassword` GET.
Confirm what conditions cause `ResetPasswordInvalid` to be returned.
Do not assume the root cause. Confirm it with evidence.

## Why the Existing Test Does Not Catch This Bug
The existing regression test seeds a single user where the domain user `Id` and the
Identity user `Id` are set to the same value. Read the `ResetPassword` GET and POST
actions carefully. Identify the code path that leads to `ResetPasswordInvalid`. Then
determine whether the existing test seed data hides that path.

## Test to Write
Add a new test to `PasswordResetRegressionTests` that:

1. Seeds a user whose data reflects the condition that causes the failure
   — read the code first to determine what that condition is, then design the seed data accordingly
2. Requests a password reset for that user via POST to `/Account/ForgotPassword`
3. Reads the generated token from the database and confirms it is valid (not expired, not used)
4. Follows the reset link via GET to `/Account/ResetPassword?token={token}`
5. Asserts the response is the reset form — NOT a redirect to `ResetPasswordInvalid`
   — this assertion will FAIL before the fix and PASS after
6. Completes the reset via POST with a new password
7. Logs in with the new password and confirms the redirect is to the correct dashboard

This test must be RED before the fix and GREEN after. That is the definition of done.

## Rules
- Do not change any production code until the test is written and confirmed red
- TDD: failing test first, then fix
- No null guards or defensive code as a first response — fix the actual cause
- Full test suite must pass after the fix with zero regressions

## Definition of Done
- [ ] New test is written and confirmed red
- [ ] Root cause confirmed from the failing test output
- [ ] Production code fixed
- [ ] New test is green
- [ ] Full test suite passes with zero regressions
- [ ] Changes committed: `bugfix: BUG-005 — fix password reset link immediately expiring`
- [ ] Merged back to `main`
- [ ] Update Tasks.md with [DONE] when complete
