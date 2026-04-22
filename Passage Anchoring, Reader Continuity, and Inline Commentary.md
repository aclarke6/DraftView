# DraftView — RSprint: Passage Anchoring, Reader Continuity, and Inline Commentary

**Date:** 2026-04-22  
**Status:** Planned (Intent-Driven, Production-Safe)  
**Execution Model:** Incremental, deployable at every sprint; phases deployable where safe  

---

# 0. Purpose

Establish a **core passage anchoring capability** that supports:

- Inline (selected text) comments  
- Cross-version comment relocation  
- Reader resume position across versions  
- Human correction (relink / reject)  
- Original context integrity  

This is a **platform capability** that underpins multiple reader and author features.

---

# 1. Operating Model

This sprint series is:

- **Intent-driven** (not schema pre-defined)
- **Discovery-led** (Copilot/Codex derives structures)
- **TDD-enforced**
- **Production-safe at every sprint boundary**

---

# 2. Deployment Rules (Critical)

## Sprint Rule (Non-Negotiable)
Every sprint must:
- compile cleanly  
- pass all tests  
- be safe for production deployment  
- not degrade existing user behaviour  

## Phase Rule
Each phase:
- should be deployable independently **where possible**
- if not deployable, must:
  - be explicitly marked as **NON-DEPLOYABLE**
  - include reason
  - define safe grouping with next phase

## Safety Principle
No sprint or phase may:
- break existing comments  
- break reading experience  
- introduce silent data corruption  
- misrepresent anchor confidence  

---

# 3. Architectural Principles (Non-Negotiable)

- Anchor identity is **content-based**, not location-based  
- Original anchor is **immutable**  
- Relocation is **derived, not authoritative**  
- Scene and chapter are **containers, not identity**  
- AI is **last-resort recovery only**  
- User relink is **highest-priority override**  
- Orphaned comments are **not deleted**  
- System must **never silently misrepresent confidence**  

---

# 4. Sprint Sequence

| Sprint | Name | Deployable | Purpose |
|-------|------|-----------|--------|
| RS-A | Anchor Foundation | ✅ | Introduce anchor model safely |
| RS-B | Anchored Resume | ✅ | Replace scroll persistence |
| RS-C | Inline Comments | ✅ | Enable text-level comments |
| RS-D | Deterministic Relocation | ✅ | Cross-version matching |
| RS-E | Human Override | ✅ | Relink and reject |
| RS-F | Original Context | ✅ | Trust and audit |
| RS-G | AI Recovery | ✅ | Optional recovery layer |
| RS-H | Reader Insight | ✅ | Progress and analytics |

---

# 5. Sprint Definitions

---

## RS-A — Anchor Foundation

### Goal
Introduce anchor capability without breaking existing comments or reading.

---

### Phase A1 — Model Discovery  
**Deployable:** ✅ (No code changes)

**Intent**
- Understand current structures

**Agent Instructions**
- Inspect Comment, ReadEvent, SectionVersion
- Propose anchor model

**Output**
- Proposal only

---

### Phase A2 — Domain Definition (TDD)  
**Deployable:** ⚠️ NON-DEPLOYABLE (partial model)

**Reason**
- Domain model incomplete without persistence

**Intent**
- Define anchor concept and rules

**Action**
- Write failing tests
- Implement minimal domain

**Deploy Note**
- Must be deployed with A3

---

### Phase A3 — Persistence  
**Deployable:** ✅

**Intent**
- Persist anchor safely

**Constraints**
- Backwards compatible
- No breaking schema changes

---

### Phase A4 — Application Surface  
**Deployable:** ✅

**Intent**
- Introduce creation/retrieval methods

**Outcome**
- Anchor exists but unused in UI

---

## RS-B — Anchored Resume

### Goal
Replace fragile scroll persistence.

---

### Phase B1 — Capture  
**Deployable:** ⚠️ NON-DEPLOYABLE (incomplete UX)

**Reason**
- Capturing without restore causes inconsistency

---

### Phase B2 — Restore  
**Deployable:** ⚠️ NON-DEPLOYABLE (requires B1)

---

### Phase B3 — Integration  
**Deployable:** ✅

**Intent**
- Fully replace scroll logic

**Outcome**
- Reader resumes reliably

---

### Phase B4 — Tests  
**Deployable:** ✅

---

## RS-C — Inline Comments

### Goal
Enable selected text comments.

---

### Phase C1 — Selection Capture  
**Deployable:** ⚠️ NON-DEPLOYABLE

**Reason**
- No persistence yet

---

### Phase C2 — Comment Creation  
**Deployable:** ⚠️ NON-DEPLOYABLE

---

### Phase C3 — Rendering  
**Deployable:** ✅

**Outcome**
- Inline comments visible

---

### Phase C4 — Tests  
**Deployable:** ✅

---

## RS-D — Deterministic Relocation

### Goal
Relocate anchors across versions.

---

### Phase D1 — Exact Matching  
**Deployable:** ⚠️ NON-DEPLOYABLE

---

### Phase D2 — Context Matching  
**Deployable:** ⚠️ NON-DEPLOYABLE

---

### Phase D3 — Fuzzy Matching  
**Deployable:** ⚠️ NON-DEPLOYABLE

---

### Phase D4 — Confidence Scoring  
**Deployable:** ⚠️ NON-DEPLOYABLE

---

### Phase D5 — Integration  
**Deployable:** ✅

**Outcome**
- Full relocation pipeline active

---

## RS-E — Human Override

### Goal
Allow correction of system matches.

---

### Phase E1 — Permissions  
**Deployable:** ✅

---

### Phase E2 — Reject Match  
**Deployable:** ⚠️ NON-DEPLOYABLE

---

### Phase E3 — Relink  
**Deployable:** ⚠️ NON-DEPLOYABLE

---

### Phase E4 — Integration  
**Deployable:** ✅

---

## RS-F — Original Context

### Goal
Preserve trust.

---

### Phase F1 — Retrieval  
**Deployable:** ⚠️ NON-DEPLOYABLE

---

### Phase F2 — Navigation  
**Deployable:** ⚠️ NON-DEPLOYABLE

---

### Phase F3 — UI Integration  
**Deployable:** ✅

---

## RS-G — AI Recovery

### Goal
Recover anchors.

---

### Phase G1 — Integration  
**Deployable:** ⚠️ NON-DEPLOYABLE

---

### Phase G2 — Confidence Handling  
**Deployable:** ⚠️ NON-DEPLOYABLE

---

### Phase G3 — Activation  
**Deployable:** ✅

---

## RS-H — Reader Insight

### Goal
Build user-facing features.

---

### Phase H1 — Progress Tracking  
**Deployable:** ⚠️ NON-DEPLOYABLE

---

### Phase H2 — Author Insight  
**Deployable:** ⚠️ NON-DEPLOYABLE

---

### Phase H3 — UI Integration  
**Deployable:** ✅

---

# 6. Behavioural Model

## Anchor Ownership
- Original anchor → immutable  
- Derived anchor → replaceable  
- Relink → authoritative override  

---

## Status Model

- Exact  
- Approximate  
- Fuzzy  
- AI Matched  
- Orphaned  
- User Relinked  
- User Rejected  

---

## Relocation Authority

Only:
- comment owner  
- author  

---

## Orphaned Behaviour

- Never deleted  
- Always visible  
- Clearly marked  

---

## Original Context

- Always accessible  
- Always authoritative  

---

# 7. Definition of Done

### Reader
- Resume reading across versions  
- Comment on selected text  

### Author
- View comments accurately  
- Access original context  

### System
- Relocate anchors safely  
- Indicate confidence  
- Allow correction  
- Never mislead  

---

# 8. First Execution Step

Start with:

**RS-A Phase A1 — Model Discovery**

Stop after proposal.

---

# 9. Success Criteria

- No regression in reading experience  
- No data loss  
- Comments survive structural edits  
- Anchors remain trustworthy  
- System degrades safely  

---

# Final Note

This design ensures:

- Every sprint is production-safe  
- Phases are clearly marked when unsafe alone  
- System evolves incrementally without risk  

This is a **foundation build**, not a feature drop.  
Precision here prevents large-scale rework later.
