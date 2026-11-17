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

## Resolved

(none yet — this project is at initial scaffolding)
