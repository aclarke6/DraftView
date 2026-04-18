---
mode: agent
description: BugFix-PC — Resolve MailKit NU1902 vulnerability warning
---

# BugFix-PC — MailKit NU1902 Vulnerability Warning

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, run terminal commands.

Before writing any code:
1. Read `.github/copilot-instructions.md`
2. Confirm the active branch is `bugfix/mailkit-nu1902`
   — if not on this branch, stop and report
3. Run `git status` — confirm the working tree is clean with no uncommitted changes
4. Run `dotnet test --nologo` and record the baseline passing count

---

## Context

Every build produces four identical warnings:

```
warning NU1902: Package 'MailKit' 4.15.1 has a known moderate severity vulnerability
https://github.com/advisories/GHSA-9j88-vvj5-vhgr
```

These appear in:
- `DraftView.Web`
- `DraftView.Web.Tests`
- `DraftView.Integration.Tests`
- `DraftView.DevTools`

This is a pre-existing warning unrelated to any V-Sprint work.

---

## Goal

Upgrade MailKit (and MimeKit if present) to the latest stable version that
resolves GHSA-9j88-vvj5-vhgr. Confirm the NU1902 warnings are gone.
Confirm no regressions.

---

## Steps

1. Check the GitHub advisory at `https://github.com/advisories/GHSA-9j88-vvj5-vhgr`
   to confirm which version resolves it.

2. Check the latest stable MailKit release at `https://www.nuget.org/packages/MailKit`

3. Update MailKit in all affected `.csproj` files:
   - `DraftView.Web/DraftView.Web.csproj`
   - `DraftView.Web.Tests/DraftView.Web.Tests.csproj`
   - `DraftView.Integration.Tests/DraftView.Integration.Tests.csproj`
   - `DraftView.DevTools/DraftView.DevTools.csproj`

4. If MimeKit is referenced separately in any project, update it to the matching
   version — MailKit and MimeKit versions must stay in sync.

5. Run `dotnet restore --nologo`

6. Run `dotnet build --nologo` — confirm NU1902 warnings are gone

7. Run `dotnet test --nologo` — confirm zero regressions

---

## Commit

Single commit:

```
chore: upgrade MailKit to resolve NU1902 vulnerability warning
```

---

## Phase Gate

- [ ] NU1902 warnings absent from build output
- [ ] `dotnet build --nologo` succeeds cleanly
- [ ] `dotnet test --nologo` — same pass count as baseline, 0 failed
- [ ] No other packages modified
- [ ] Committed to `bugfix/mailkit-nu1902`

---

## Constraints

- Do not upgrade any package other than MailKit and MimeKit
- Do not modify any application code
- Do not modify any test logic
