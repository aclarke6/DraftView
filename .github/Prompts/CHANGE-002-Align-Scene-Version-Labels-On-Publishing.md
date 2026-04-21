mode: agent
description: CHANGE-002 — align scene version labels beside scene titles in Author Publishing

Branching
- Create branch: change/CHANGE-002-scene-version-alignment
- Branch from: main

Change

In Author Publishing, scene version labels (v1, v2) are rendered too far to the right.

Move the version label so it sits directly beside the scene title.

The layout must use fixed columns so:
- version labels align vertically across all rows
- action buttons remain aligned
- longer scene titles do not push version labels out of position

This is a layout and rendering issue only.

Do not change:
- versioning logic
- controller logic
- domain/application/infrastructure behaviour

Expected outcome

- Scene title column (left aligned)
- Version label column (fixed position beside title)
- Action column (unchanged alignment)
- Clean, consistent row layout regardless of title length

Files to review

- DraftView.Web/Views/Author/Publishing.cshtml
- DraftView.Web/wwwroot/css/DraftView.Core.css
- DraftView.Web/wwwroot/css/DraftView.Components.css
- DraftView.Web/wwwroot/css/DraftView.Dashboard.css
- DraftView.Web.Tests/Controllers/AuthorControllerTests.cs

Instructions

- Read current Publishing.cshtml markup
- Identify how scene rows are structured
- Introduce minimal structural change if required
- Prefer CSS-based column alignment (flex or grid)
- Do not use inline styles
- Do not redesign the page
- Reuse existing CSS classes where possible
- Only add CSS if a clear location exists in current stylesheets

What to produce first

Produce a 4-section plan only. Stop after the plan.

Section 1 — Current State Analysis
- where scene titles render
- where version labels render
- why labels drift to RHS
- what layout structure exists today

Section 2 — Failing Test Plan
- tests to verify version label positioning data is present
- ensure no regression to publishing classification
- explain why tests are red now
- define what green looks like

Section 3 — Proposed Fix
- exact files to change
- view vs CSS responsibilities
- how column alignment will be achieved
- confirm no logic changes
- confirm no migration

Section 4 — Success Gates
- tests red before fix
- tests green after fix
- full suite passes
- browser verification:
  - version labels aligned beside titles
  - alignment stable across varying title lengths
  - actions unaffected
- commit to change branch
- TASKS.md updated if required
- present merge commands only