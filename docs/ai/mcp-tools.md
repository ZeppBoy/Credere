# MCP tool catalog

Six tools exposed by `Credere.Mcp`. All are read-only for M5. Tool implementations live in `src/Credere.Modules.Ai/Application/Mcp/`; the Mcp host just adapts transport to `IAiModule.InvokeMcpToolAsync`.

All tool invocations:

- Require a JWT bearer with a declared scope.
- Emit a Langfuse span with tool name, args (PII redacted), latency, result hash.
- Append to `ai.llm_calls` and `ai.tool_invocations` collections.
- Return structured JSON matching the tool's declared return schema.

## 1. `get_loan_by_id`

**Scope:** `banker:read`
**Purpose:** Fetch the full servicing view of a loan.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `loan_id` | string (Guid) | yes | The loan id. |

**Returns:**

```json
{
  "id": "...",
  "state": "Active | InGrace | PastDue | Defaulted | ...",
  "principal": { "amount": 10000.00, "currency": "EUR" },
  "remaining_principal": { "amount": 8500.00, "currency": "EUR" },
  "fees_outstanding": { "amount": 0.00, "currency": "EUR" },
  "past_due_interest": { "amount": 0.00, "currency": "EUR" },
  "current_interest": { "amount": 42.17, "currency": "EUR" },
  "rate": { "type": "fixed", "nominal": 5.25, "day_count": "30E/360" },
  "term_months": 36,
  "disbursement_date": "2026-01-15",
  "first_payment_date": "2026-02-15",
  "days_past_due": null,
  "aprc": 5.49
}
```

**Not found:** structured error `{ "ok": false, "error": { "code": "not-found", "message": "Loan {id} not found" } }`.

## 2. `search_loans`

**Scope:** `banker:read`
**Purpose:** Filter loans in the portfolio. Thin wrapper over `IReportingModule.ListLoansAsync`.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `state` | string | no | One of the loan states. |
| `min_days_past_due` | int | no | Lower bound on days past due. |
| `max_days_past_due` | int | no | Upper bound. |
| `product_code` | string | no | Product filter. |
| `limit` | int | no | Max rows returned (default 50, cap 500). |
| `cursor` | string | no | Opaque pagination cursor. |

**Returns:** `{ "rows": [LoanRow, ...], "next_cursor": "..." | null }`

## 3. `calculate_payoff`

**Scope:** `banker:simulate`
**Purpose:** Produce a payoff quote as of a given date without executing.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `loan_id` | string (Guid) | yes | |
| `as_of` | string (date) | yes | Business-effective payoff date. |

**Returns:**

```json
{
  "loan_id": "...",
  "as_of": "2026-11-17",
  "remaining_principal": { ... },
  "accrued_interest_to_date": { ... },
  "fees_outstanding": { ... },
  "total_payoff": { ... },
  "breakdown": [
    { "component": "principal", "amount": { ... } },
    { "component": "interest", "amount": { ... } },
    { "component": "fees", "amount": { ... } }
  ]
}
```

**Error cases:**
- Loan in terminal state → `{ "code": "invalid-state-transition", ... }`.
- Date in the past → `{ "code": "validation", "field": "as_of", "message": "..." }`.

## 4. `get_schedule`

**Scope:** `banker:read`
**Purpose:** Return the loan's amortization schedule.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `loan_id` | string (Guid) | yes | |
| `remaining_only` | bool | no | If true, return only rows with `PaymentDate >= today`. |

**Returns:** `AmortizationSchedule` as JSON.

## 5. `get_aging_status`

**Scope:** `banker:read`
**Purpose:** Compact aging summary for a loan.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `loan_id` | string (Guid) | yes | |

**Returns:**

```json
{
  "loan_id": "...",
  "state": "PastDue",
  "days_past_due": 15,
  "last_payment_date": "2026-10-15",
  "last_payment_amount": { "amount": 293.65, "currency": "EUR" },
  "next_scheduled_date": "2026-11-15",
  "next_scheduled_amount": { "amount": 293.65, "currency": "EUR" },
  "ifrs9_stage": 1
}
```

## 6. `list_overdue`

**Scope:** `banker:read`
**Purpose:** List loans currently past due or defaulted.

**Parameters:**

| Name | Type | Required | Description |
|---|---|---|---|
| `min_days_past_due` | int | no | Default 1. |
| `limit` | int | no | Default 50, cap 500. |

**Returns:** array of `AgingStatus`-shaped records (same schema as `get_aging_status`).

## Tool registration

The Mcp host registers the catalog at startup:

```csharp
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<GetLoanByIdTool>()
    .WithTools<SearchLoansTool>()
    .WithTools<CalculatePayoffTool>()
    .WithTools<GetScheduleTool>()
    .WithTools<GetAgingStatusTool>()
    .WithTools<ListOverdueTool>();
```

Each tool is a thin adapter that validates args, calls `IAiModule.InvokeMcpToolAsync`, and returns the result.

## Authorization

Every tool declares a required scope in its metadata. The Ai module's dispatcher:

1. Validates the JWT passed in by `Credere.Mcp` (or in the HTTP transport later).
2. Checks that the JWT carries the required scope.
3. Rejects the call with a structured error on auth failure.

Unauthorized invocations are logged with `{ tool, caller, missing_scope }` but do not throw.

## Telemetry

Each invocation records:

- `tool_name`
- `caller` (JWT sub)
- `args_hash` (SHA-256 of canonical JSON — not the args themselves)
- `latency_ms`
- `result_hash`
- `cost_usd` (if Langfuse enrichment available)
- `langfuse_trace_id`

## Testing

- **Unit tests per tool** cover: valid args → correct result shape; invalid args → `ValidationError`; auth failure → structured error.
- **Integration tests** run tools end-to-end against a seeded Testcontainers SQL Server.
- **Manual smoke** from Claude Desktop is documented in a `README-manual-test.md` under `src/Credere.Mcp/` (created in M5).
