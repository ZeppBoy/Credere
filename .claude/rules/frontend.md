---
paths:
  - "web/**"
  - "docs/frontend.md"
---

# Frontend (web/)

`docs/frontend.md` is the full description. These are the working rules.

- Components are Server Components by default. Use `"use client"` only where interactivity needs it.
- Business logic lives in `Credere.Api`. The Next.js `app/api/` routes exist only for auth and token-forwarding proxies, so the access token never reaches the browser.
- Styling is Tailwind v4 with CSS variables. shadcn/ui components are copied into `components/ui/` and may be edited freely.
- There is no global state. Use TanStack Query only on pages that need a client-side cache.
- API DTO types are generated from `Credere.Api`'s OpenAPI output. The generator choice is open question #6, and the OIDC provider is open question #3; ask the owner before picking either.
- Packages are installed with pnpm via Corepack. A new npm package needs an ADR.
- Check frontend work with `pnpm --dir web lint` and `pnpm --dir web build`. Playwright E2E tests arrive in M3.
