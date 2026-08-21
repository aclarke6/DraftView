# Claude Design Brief — DraftView Marketing Pages

## Project

Design four static marketing pages for **DraftView** — a beta reader platform for
fiction authors. The pages will be used to explain the product's value to authors
and readers and drive sign-ups.

---

## Brand

**Name:** DraftView
**Tagline:** *Your novel. Your readers. Your control.*
**Tone:** Literary, considered, warm — not startup-generic. This is a product for
authors who care about their craft.
**Colour palette:** Draw from the existing images and CSS — deep navy/slate, warm gold,
aged parchment cream. Dark mode-capable.
**Typography:** Serif for headings (literary feel), clean sans-serif for body.

---

## Images and CSS

All assets are already live. Reference these URLs directly — do not upload files.

**Use the web capture tool to read the live CSS first:**
`https://draftview.co.uk/css/DraftView.Core.css`

This contains all colour variables, spacing tokens, typography scale, and component
styles. Apply these tokens throughout all four pages so the marketing pages are
visually consistent with the live application.

| URL | Description | Use |
|---|---|---|
| `https://draftview.co.uk/images/DraftView.Header.web.jpg` | Abstract dark navy with sweeping gold lines and star-like light points | Hero backgrounds — landing page and author features |
| `https://draftview.co.uk/images/DraftView.Atmosphere.web.jpg` | Illustrated writer's desk — open manuscript, quill, inkwell, brass lamp, warm amber light | Atmosphere section — evokes the act of writing |
| `https://draftview.co.uk/images/DraftView.Tile.web.jpg` | Aged parchment cream texture | Section backgrounds, cards, subtle texture breaks |
| `https://draftview.co.uk/images/DraftView.Tile.Dark.web.jpg` | Dark version of the tile | Dark section backgrounds |
| `https://draftview.co.uk/images/DraftView.Logo.transparent.png` | DraftView logo with transparent background | Nav, footer, hero overlay |

---

## Page 1 — Landing / Home Page (author-facing)

**Hero:** Full-width `DraftView.Header.web.jpg` background. Logo top-left. Nav:
Features / For Readers / Pricing / Sign In. Large headline, subheading, two CTAs.

**Headline:** *Your novel deserves the right readers.*
**Subheading:** *DraftView gives authors a private, controlled space to share chapters
with trusted beta readers — and get the feedback that shapes a better book.*
**CTAs:** `Start for free` (primary) and `See how it works` (secondary)

**Section 2 — How it works** (three steps, parchment tile background):
1. *Publish your chapters* — Sync from Scrivener via Dropbox, or upload directly.
   You decide what readers see and when.
2. *Invite your readers* — Share a private invitation. Readers access only what
   you've published.
3. *Gather feedback* — Readers comment scene by scene. You read, respond, and revise
   with confidence.

**Section 3 — Atmosphere** (`DraftView.Atmosphere.web.jpg` full-width, overlaid text):
*"Write with the freedom to share. Share with the control to revise."*

**Section 4 — Feature highlights** (two columns, dark background):
- For Authors: versioning, publishing control, Scrivener sync, reader management
- For Readers: clean reading experience, scene-level comments, update notifications

**Footer:** Logo, nav links, `draftview.co.uk`

---

## Page 2 — Author Features Page

**Hero:** `DraftView.Header.web.jpg`. Headline: *Built for how authors actually work.*

**Feature sections** (alternating layout, parchment tile accents):

1. **Publish when you're ready** — Chapters only go live when you publish them. Sync
   from Scrivener via Dropbox keeps your working draft private until you decide to share.

2. **Version control that makes sense** — Every time you republish a chapter, DraftView
   creates a new version. Readers see what changed. You see how your manuscript is evolving.

3. **Lock chapters while you revise** — Revising a chapter mid-read? Lock it. Readers
   see a polite notice. Unlock when you're ready.

4. **Understand your changes before you share** — DraftView classifies every republish
   as a Polish, Revision, or Rewrite — so you always know the weight of what you're sharing.

5. **AI summaries for your readers** — When you republish, DraftView generates a
   one-line summary of what changed — naming characters, places, events — so readers
   know what's new without rereading from scratch.

6. **Manage your readers** — Invite by email, control project access, deactivate when
   beta reading is complete.

---

## Page 3 — Beta Reader Features Page

**Hero:** `DraftView.Atmosphere.web.jpg` (cropped/overlaid).
Headline: *Read the story as it's meant to be read.*

**Feature sections:**

1. **A clean reading experience** — No distractions. Just the prose, beautifully
   typeset. Choose your font and size. Works on desktop and mobile.

2. **Comment as you read** — Leave comments scene by scene. React to moments as they
   happen. The author sees everything.

3. **Know when chapters have changed** — A clear update banner tells you when a chapter
   you've read has been revised, and gives you a summary of what changed. No guesswork.

4. **Your progress, your pace** — DraftView remembers where you left off. Pick up
   exactly where you stopped.

5. **Private by design** — Your email is never shared. Your comments are only visible
   to you and the author.

---

## Page 4 — Pricing Page

**Hero:** Simple — dark navy, logo, headline: *Simple pricing. No surprises.*

**Tiers** (card layout, parchment tile background):

| | Free | Author |
|---|---|---|
| **Price** | £0 forever | £X/month |
| Projects | 1 | Unlimited |
| Beta readers | Up to 3 | Unlimited |
| Version history | Latest only | Full history |
| AI summaries | — | ✓ |
| Scrivener sync | ✓ | ✓ |
| Manual upload | ✓ | ✓ |

*(Replace £X with your planned pricing before finalising)*

**CTA below cards:** `Start for free — no credit card required`

**Footer:** same as landing page

---

## Design Notes for Claude Design

- Maintain visual consistency across all four pages — same nav, same footer, same
  type scale
- Dark navy + gold is the primary palette; aged parchment cream for content sections
- The images have a literary/fantasy quality — lean into that, not a clinical SaaS
  aesthetic
- No stock photography — use the provided image URLs only
- Apply CSS variables from `DraftView.Core.css` throughout — do not hardcode colours
  or spacing values that already exist as tokens
- Export each page as a standalone HTML file so they can be integrated directly into
  DraftView as static Razor views

---

## How to Use This Brief

1. Go to `claude.ai/design`
2. Start a new project
3. Use the web capture tool to read `https://draftview.co.uk/css/DraftView.Core.css`
4. Paste this brief and ask Claude Design to start with Page 1
5. Iterate on Page 1 until satisfied, then move to Page 2, and so on
6. Export each page as HTML when complete
