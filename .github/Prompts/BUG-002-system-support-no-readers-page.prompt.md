---
mode: agent
description: BUG-002 — System Support has no readers page
---

# BUG-002 — System Support has no readers page

## Branching
Branch from `BugFix-Mac` (or `BugFix-PC` if working on Windows):
`bugfix/BUG-002-system-support-no-readers-page`

## Problem
There is no UI surface for SystemSupport to view readers. This means the
deny-by-default email behaviour for the SystemSupport role cannot be verified
through the UI, and the Sprint 4 UAT checklist item cannot be completed.

## Decision Required Before Implementing


- Add a dedicated SystemSupport readers page at `/Support/Readers`
- Shows reader list with display names only (no email addresses)
- Email access denied by default — SystemSupport must go through the controlled
  privileged access path to see an email
- Proves the deny-by-default behaviour through the UI



## Option A Implementation

### Before writing any code, read:
1. `DraftView.Web/Controllers/SupportController.cs` — understand existing Support surface
2. `DraftView.Web/Views/Support/` — understand existing Support views
3. `DraftView.Application/Services/UserService.cs` — `GetAllBetaReadersAsync`
4. `DraftView.Domain/Interfaces/Services/IUserEmailAccessService.cs` — access control

### Deliverable
- `GET /Support/Readers` — lists readers with display names only
- Email column absent — email access requires explicit privileged request
- `[Authorize(Roles = "SystemSupport")]` on the action
- No new CSS files — use existing styles

### Rules
- No email addresses visible on the page
- Deny-by-default: email not shown unless explicitly authorised through controlled access path
- TDD for any application-layer changes

## Commit
- `feat: BUG-002 — add SystemSupport readers page with deny-by-default email`
- Merge back to `BugFix-Mac` or `BugFix-PC` when complete
