# AGENTS.md

This file provides guidance for AI agents working in the `web/landing` project.

## Project

React 19 + Vite SPA (the HTWind marketing/landing site). Rendered client-side
and deployed to Vercel. i18n is handled with i18next; 18 locales live under
`src/locales/*.json`.

## Commands

Run all commands from `web/landing`.

- **Install:** `bun install`
- **Dev:** `bun run dev`
- **Build:** `bun run build` (runs `tsc -b && vite build`)
- **Lint:** `bun run lint`
- **Type + lint + format check:** `bun run check`

Always run `bun run check` after editing TypeScript/React files, and
`bun run build` before considering a change shippable.

## SEO & i18n conventions

- Every locale URL follows the pattern `/<locale>/` (e.g. `/tr/`, `/ar/`).
  The root `/` is the `x-default` fallback.
- `<html lang>` and `<html dir>` are managed at runtime by
  `src/hooks/useHeadMetadata.ts`. RTL locales (currently `ar`) get `dir="rtl"`.
- Canonical, hreflang, Open Graph, Twitter, and JSON-LD structured data are
  injected client-side by `useHeadMetadata`. Static fallbacks for crawlers and
  no-JS users live in `index.html` (`<noscript>` block + base JSON-LD).
- `public/sitemap.xml` must contain reciprocal hreflang alternates for every
  locale URL, plus the `x-default` root entry. When a locale is added or removed,
  update: `src/i18n/locales.ts`, `src/i18n/resources.ts`, `src/locales/*.json`,
  `public/sitemap.xml`, `public/robots.txt` (no change needed), `index.html`
  static hreflang links, and the `<noscript>` language list.
- Accessibility: decorative icons use `aria-hidden="true" focusable="false"`;
  external links use `rel="noopener noreferrer"`; the page exposes a
  skip-to-content link targeting `#main-content`.

## Accessibility

- Keep one `<h1>` per page (hero heading).
- Section headings are `<h2>`; FAQ questions are `<h3>`.
- All interactive elements must have an accessible name; images need
  descriptive `alt` text.
