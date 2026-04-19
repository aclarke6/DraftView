---
mode: agent
description: Bug Fix — diff-para--removed renders deleted text instead of a marker
---

# Bug Fix — diff-para--removed UX

## Agent Instructions

Use **Claude Sonnet 4.5** as the model for this session.
Operate in **agent mode** — read existing files, create new files, run terminal commands.

Before writing any code:
1. Read `REFACTORING.md` in full
2. Read `.github/copilot-instructions.md`
3. Read `DraftView.Web/Views/Reader/DesktopRead.cshtml` — locate the diff-para--removed render block
4. Read `DraftView.Web/Views/Reader/MobileRead.cshtml` — locate the diff-para--removed render block
5. Read `DraftView.Web/wwwroot/css/DraftView.DesktopReader.css` — locate the `.diff-para--removed` rule
6. Confirm the active branch is `bugfix/diff-para-removed`
   — if not on this branch, stop and report
7. Run `dotnet test --nologo` and record the baseline passing count before touching any code

---

## Problem

Removed paragraphs in the reader diff view currently render their full HTML content
with a strikethrough style. This pulls reader attention to deleted prose rather than
the published content they should be reading.

Beta readers are responding to the manuscript as readers, not reviewing tracked changes.
Their role is to engage with what the author has written, not to assess what was removed.

---

## Fix

### Views — render nothing for removed paragraphs

**File:** `DraftView.Web/Views/Reader/DesktopRead.cshtml`

Find the removed paragraph branch in the diff render block:

```razor
else if (para.Type == DiffResultType.Removed)
{
    <div class="diff-para diff-para--removed">@Html.Raw(para.Html)</div>
}
```

Replace with an empty marker div — no content, no HTML output:

```razor
else if (para.Type == DiffResultType.Removed)
{
    <div class="diff-para diff-para--removed"></div>
}
```

**File:** `DraftView.Web/Views/Reader/MobileRead.cshtml`

Apply the identical change to the mobile diff render block.

Style leakage audit: confirm no `style=""` attributes introduced in either view.

---

### CSS — thin left bar only, no content styling

**File:** `DraftView.Web/wwwroot/css/DraftView.DesktopReader.css`

Find the existing `.diff-para--removed` rule and replace it entirely:

```css
/* Removed paragraph marker — DesktopRead.cshtml / MobileRead.cshtml
   Renders as a thin red left bar only. Content is intentionally hidden —
   beta readers focus on what is present, not what was deleted. */
.diff-para--removed {
    border-left: 3px solid var(--color-diff-removed, #ef4444);
    height: var(--space-3, 12px);
    margin-bottom: var(--space-2);
    opacity: 0.5;
}
```

Remove any existing `text-decoration: line-through`, `background`, `padding-left`,
or content-related properties from the `.diff-para--removed` rule. The element must
render as a visual gap marker, not as a content block.

Bump the CSS version token in `DraftView.Core.css` using regex replace.
Update the CSS version in `_Layout.cshtml` to match.

---

## Tests

No new tests are required — this is a pure view and CSS change with no domain,
application, or infrastructure impact.

Run the full test suite to confirm zero regressions.

---

## Phase Gate — All Must Pass Before Marking Complete

Run `dotnet test --nologo` and confirm:

- [ ] Total passing count equal to baseline — zero regressions
- [ ] Solution builds without errors
- [ ] `DesktopRead.cshtml` removed branch renders empty div with no HTML content
- [ ] `MobileRead.cshtml` removed branch renders empty div with no HTML content
- [ ] `.diff-para--removed` CSS shows only a left border — no text-decoration, no background tint on content
- [ ] CSS version token bumped
- [ ] No inline styles introduced in either view
- [ ] Style leakage audit completed on both modified views
- [ ] TASKS.md bug updated from `[OPEN]` to `[DONE]` with resolution note
- [ ] All changes committed to `bugfix/diff-para-removed`
- [ ] No warnings in test output linked to changes

---

## Identify All Warnings in Tests

Run `dotnet test --nologo` and identify any warnings in the test output.
Address any warnings that are linked to code changes made in this fix before
proceeding.

---

## Refactor Phase

After implementing the above, consider if any refactor is needed to improve code
quality, as per the refactoring guidelines. If so, perform the refactor and ensure
all tests still pass.

---

## Do NOT implement in this fix

- Any changes to the `diff-para--added` style
- Any changes to how diffs are computed
- Any changes to ViewModels or controller logic
- Any changes outside the three named files plus the CSS version token
