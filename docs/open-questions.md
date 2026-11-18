# Open questions

Questions that are undecided. **Replaces "proposed" ADRs.** When a question is answered, the entry moves to an accepted ADR in `adr/` and this file links to it.

Each entry has: question, options considered, owner, by-when (if any).

## Active

### 1. How does the AI module call Servicing / Origination?

**Context.** Agents and MCP tools need to read loan and application data. They must not reference another module's internals.

**Options.**
- (a) `IAiModule` depends on `IServicingModule` + `IOriginationModule` via DI. Simplest. Tightly couples Ai to internal module APIs.
- (b) MCP tools call `Credere.Api` HTTP endpoints. Clean, but adds HTTP latency inside the same process.
- (c) Reporting module is the sole read-side; Ai depends only on `IReportingModule`. Reporting is already a pure event consumer.

**Owner.** Human, before M5 kickoff.

**Recommendation.** (c). Reporting is the canonical read side; everything agents need is already projected there. Falls back to (a) for anything not in Reporting yet.

---

### 2. MCP transport: stdio or HTTP first?

**Context.** stdio is required for Claude Desktop local usage. HTTP is required for the Banker UI's inline agent panels.

**Options.**
- (a) stdio only in M5; add HTTP in M6 when the UI needs it. Keeps M5 small.
- (b) HTTP from the start. Cleaner single code path; one extra week for TLS, auth middleware.
- (c) Both from the start.

**Owner.** Human, before M5 kickoff.

**Recommendation.** (a).

---

### 3. Identity provider in dev — local IdentityServer, Keycloak, or a hand-rolled JWT issuer?

**Context.** NextAuth needs an OIDC provider. `Credere.Api` and `Credere.Mcp` need JWT validation. For a portfolio demo we want zero external accounts.

**Options.**
- (a) Duende IdentityServer (local, dev-only). Ergonomic; commercial-license concerns in prod.
- (b) Keycloak in Docker. Full-featured, heavy container.
- (c) OpenIddict (open-source ASP.NET OIDC server). Light, self-hosted.
- (d) Hand-rolled JWT issuer using `Microsoft.IdentityModel.Tokens`. Smallest; least realistic.

**Owner.** Human, before M3 kickoff.

**Recommendation.** (c) OpenIddict. Open source, light, demonstrates OAuth2 correctly without licensing friction.

---

### 4. Batch scheduling: hosted-service interval, cron syntax, or Hangfire?

**Context.** The daily accrual + aging job plus the outbox dispatcher need to run on a schedule.

**Options.**
- (a) `BackgroundService` with a fixed interval. Simple; crude.
- (b) `BackgroundService` with cron parsing (NCrontab or Cronos). Clean; small dependency.
- (c) Hangfire. Rich dashboard; heavy; introduces SQL-based job queue.

**Owner.** Human, before M4 kickoff.

**Recommendation.** (b).

---

### 5. Document storage: blobs in Mongo, or object storage (S3 / MinIO) later?

**Context.** PDFs are currently planned for Mongo `documents.blobs`. For a real system, object storage is standard; for a portfolio demo Mongo is cheaper to operate.

**Options.**
- (a) Mongo blobs from the start. Keeps the compose file lean.
- (b) MinIO in compose from the start. Closer to production but adds a container.
- (c) Mongo in M3, migrate to MinIO in a later milestone if it becomes interesting.

**Owner.** Human, before M3 kickoff.

**Recommendation.** (c).

---

### 6. OpenAPI-to-TypeScript generation

**Context.** The Next.js app needs typed API clients. Options range from hand-written types to code generation.

**Options.**
- (a) Hand-written types in `web/lib/types.ts`. Low cost today, drifts later.
- (b) `openapi-typescript` generation from `Credere.Api`'s swagger output. One toolchain step.
- (c) tRPC or OpenAPI-generator with a full fetch client.

**Owner.** Human, before M3 UI kickoff.

**Recommendation.** (b).

---

### 7. Does the `Microsoft.Extensions.AI` provider support what Claude Sonnet 5.5 requires?

**Context.** The agents run on a hand-rolled loop over `IChatClient` (locked stack). Claude Sonnet 5.5 requires the loop to:
- pass thinking blocks back unchanged, signatures included
- send `output_config.effort`
- use structured outputs (`output_config.format`)
- expose `stop_reason: "refusal"` with its `stop_details` category

If the provider behind `IChatClient` drops or can't express any of these, the loop either fails (a 400 on history edits) or loses the audit data that [`ai/agents.md` § Model requirements](ai/agents.md#model-requirements-claude-sonnet-55) depends on.

**Options.**
- (a) Use an MEAI provider that round-trips all four, configuring Anthropic-specific options through its extension points.
- (b) Keep `IChatClient` for simple calls, and call the official Anthropic C# SDK directly inside `AgentHost`. This needs an ADR, because it changes the locked stack.

**Owner.** Human, before M5. A spike settles it: send one multi-turn tool-use conversation through the chosen provider and check all four requirements.

**Recommendation.** Run the spike first. Prefer (a) if it passes; otherwise use (b), limited to `AgentHost`.

---

### 8. Where do shared persistence converters live?

**Context.** Every module needs EF Core value converters (and Mongo serializers) for NodaTime types, `Money`, and strongly typed ids. The architecture tests forbid `Credere.Core` from referencing EF or Mongo, and forbid `Credere.Shared` from referencing NodaTime, EF or Mongo.

**Options.**
- (a) Duplicate the converters in each module's `Infrastructure/`. No new project, but five copies.
- (b) Add a `Credere.Persistence` project (Core + EF + Mongo) that modules may reference. One copy, but it needs an ADR and an architecture-test update.

**Owner.** Human, before the first `DbContext` (M2).

**Recommendation.** (a) until a second module needs the converters, then (b).

---

### 9. APRC: counting a month back from a day the earlier month doesn't have

**Context.** Directive (EU) 2023/2225, Annex III, Part I, remark (c)(ii), counts whole periods backwards from a cash flow to the initial drawdown. It doesn't say what one month before, say, 31 March is. [`domain/aprc.md`](domain/aprc.md#time-intervals) uses NodaTime's `PlusMonths` clamping as an interim rule. Under it, drawdown 31 Jan 2026 → payment 28 Feb 2026 is 28/365 = 0.0767 years, not 1/12 = 0.0833. A schedule with month-end payment dates would get a broken period in every short month.

**Options.**
- (a) Keep the clamp. Simple, but a month-end payment in a shorter month never counts as a whole month.
- (b) Add an end-of-month rule: when both dates are the last day of their month, count a whole month.
- (c) Follow published guidance on 2023/2225 (Commission guidance or a Member State's transposition) if any addresses it.

**Owner.** Human, before `AprcCalculator` is implemented (M1).

**Recommendation.** Look for (c) first. If nothing addresses it, use (b) and mark it a Credere convention in the doc.

---

### 10. Fractional powers outside the APRC solver

**Context.** `EffectiveRate` interest, `(1 + R)^τ`, and its annuity periodic rate, `(1 + R)^(1/12) − 1`, both need a fractional exponent ([`domain/rates.md`](domain/rates.md)). `decimal` has no fractional power. AGENTS.md §7 allows `double` only for the exponent inside the APRC solver.

**Options.**
- (a) Extend the AGENTS.md §7 exception to `EffectiveRate`, converting back to `decimal` at one boundary, as the solver does. Little code, but every exception weakens the rule.
- (b) Add a `decimal` power function (exp/ln series) to `Credere.Core`, with its own reference cases and mutation coverage. No `double` anywhere; it could also replace the solver's `double`.
- (c) Leave `EffectiveRate` out of M1 and add it when a product needs it.

**Owner.** Human, before `EffectiveRate` is implemented (M1).

**Recommendation.** (c) for M1, then (b) when a product needs an effective-rate quote.

---

## Resolved

(none yet — this project is at initial scaffolding)
