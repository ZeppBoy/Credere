# Frontend — Banker Workbench

The `web/` directory contains a Next.js 15 (App Router) app that serves as a banker-facing workbench. Scope in M3: full underwriting + servicing flow for a demo audience. Scope in M6: inline AI recommendations.

## Stack

- **Framework:** Next.js 15, App Router, TypeScript strict mode.
- **Styling:** Tailwind CSS v4 + shadcn/ui (copy-in components, not a runtime dependency).
- **Data fetching:** React Server Components for listing/detail pages; client components only where interactivity is required.
- **Auth:** NextAuth with an OIDC provider pointing at local IdentityServer (dev). JWT access token is attached to API calls.
- **State:** none global in M3. If a page needs it, add TanStack Query locally.
- **Package manager:** pnpm (via Corepack).
- **Testing:** Playwright for the primary end-to-end flow in M3; expanded in M6.

## Routes (planned by milestone)

| Route | Milestone | Purpose |
|---|---|---|
| `/` | M3 | Dashboard: overdue, in-grace, upcoming disbursements. |
| `/applications` | M3 | List applications with filters (status, submitted date, product). |
| `/applications/[id]` | M3 | Detail: terms, applicant snapshot; approve/decline button. |
| `/applications/[id]` (+ AI panel) | M6 | Inline credit-decisioning recommendation with rationale. |
| `/loans` | M3 | List loans with filters. |
| `/loans/[id]` | M3 | Detail: lifecycle timeline, schedule, documents, record-payment action. |
| `/loans/[id]/payoff` | M3 | Payoff quote + execute. |
| `/documents/[id]` | M3 | Embedded PDF viewer. |
| `/watchlist` | M6 | Early-warning agent recommendations. |
| `/collections/[loanId]` | M6 | Collections agent outreach draft (requires approval before sending). |

## API contract

All API calls target `Credere.Api` under its base URL (configured as `NEXT_PUBLIC_API_BASE`). Server components fetch directly with the session's bearer token. Client components call through a thin `lib/api.ts` wrapper.

Endpoints and their shapes live in the module docs under [`../docs/modules/`](../docs/modules/).

## Authentication flow

```
Banker browser → /api/auth/signin (NextAuth)
NextAuth → IdentityServer OIDC authorize → consent → callback with code
NextAuth exchanges code → access_token (JWT, scope=banker:*)
Server components read token from session and forward as Authorization: Bearer
Client components call /api/proxy routes which forward to Credere.Api with the token
```

## Project layout

```
web/
├── app/
│   ├── layout.tsx                      Root layout.
│   ├── page.tsx                        Landing (M3 replaces with dashboard).
│   ├── applications/
│   ├── loans/
│   ├── documents/
│   └── api/
│       └── auth/[...nextauth]/route.ts NextAuth handler.
├── components/
│   ├── ui/                             shadcn/ui components (Button, Card, Table, ...).
│   └── domain/                         Shared feature components.
├── lib/
│   ├── api.ts                          API client wrapper.
│   └── auth.ts                         NextAuth config.
├── public/
├── .eslintrc.json
├── next.config.ts
├── package.json
├── postcss.config.js
├── tailwind.config.ts
└── tsconfig.json
```

## Conventions

- **Server-first.** Default to server components. Opt into `"use client"` only when needed.
- **No API routes for business logic.** Business logic lives in `Credere.Api`. Next.js `api/` routes exist only for auth and for secure proxying of requests that must not expose the access token to the client.
- **Tailwind v4 + CSS variables for theming.** No CSS-in-JS. No Emotion. No styled-components.
- **shadcn/ui components are copied in, not imported from a package.** Edit them freely.
- **TanStack Query only where interactivity needs client-side cache.**
- **Types for API DTOs are generated from the OpenAPI spec** emitted by `Credere.Api` (tooling introduced in M4).

## Running

```sh
corepack enable pnpm
pnpm --dir web install
pnpm --dir web dev      # http://localhost:3000
```

Required environment variables (`web/.env.local`, not committed):

```
NEXT_PUBLIC_API_BASE=http://localhost:5000
NEXTAUTH_URL=http://localhost:3000
NEXTAUTH_SECRET=<dev secret>
IDENTITY_SERVER_URL=http://localhost:5001
IDENTITY_SERVER_CLIENT_ID=credere-banker
IDENTITY_SERVER_CLIENT_SECRET=<dev secret>
```
