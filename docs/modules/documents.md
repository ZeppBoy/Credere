# Module — Documents

**Project:** `src/Credere.Modules.Documents/`
**Schema:** `documents` (SQL), `documents.blobs` (Mongo)
**Public contract:** `IDocumentsModule`

## Bounded context

Documents generates PDFs and stores them. There are three document types in scope for M3:

1. **Loan agreement** — produced automatically on `LoanDisbursed`.
2. **Amortization schedule** — produced on demand (API endpoint).
3. **Payment receipt** — produced automatically on `PaymentRecorded`.

Signed-document storage (DocuSign-style) is **out of scope**. The PDFs are rendered but we do not capture signatures; the loan agreement carries a placeholder "signed electronically" block.

## Public interface

```csharp
public interface IDocumentsModule
{
    Task<Result<DocumentId, DomainError>> GenerateLoanAgreementAsync(
        GenerateLoanAgreement cmd, CancellationToken ct);

    Task<Result<DocumentId, DomainError>> GenerateScheduleAsync(
        GenerateSchedule cmd, CancellationToken ct);

    Task<Result<DocumentId, DomainError>> GenerateReceiptAsync(
        GenerateReceipt cmd, CancellationToken ct);

    Task<DocumentMetadata?> GetMetadataAsync(DocumentId id, CancellationToken ct);
    Task<Stream?> OpenBlobAsync(DocumentId id, CancellationToken ct);

    Task<IReadOnlyList<DocumentMetadata>> ListForLoanAsync(
        LoanId loanId, CancellationToken ct);
}
```

## Integration events

### Published

| Event | When | Payload |
|---|---|---|
| `DocumentGenerated` | A generation command succeeds | DocumentId, loanId, docType, hash, byteLength, generated-at |

### Consumed

| Event | Handler |
|---|---|
| `LoanDisbursed` | Generate the loan agreement, store it, publish `DocumentGenerated`. |
| `PaymentRecorded` | Generate the payment receipt, store it, publish `DocumentGenerated`. |

## Generation — QuestPDF

Each document type lives in `src/Credere.Modules.Documents/Rendering/<Type>/`. Each is a QuestPDF `IDocument` implementation with a strongly-typed input model. No HTML intermediate step; we render directly.

Document design must:

- Include the regulatory disclosures per EU Consumer Credit Directive 2008/48/EC Article 10 (information to be included in credit agreements) for the loan agreement.
- Include the APRC with its calculation reference.
- Include the amortization schedule for the loan agreement.
- Include a `Credere | document type | loan id | version` footer with page numbers.

## Storage

**SQL (`documents.Documents`)** — one row per generated document, holding metadata:

| Column | Purpose |
|---|---|
| `Id` | `DocumentId` primary key. |
| `LoanId` | FK-like reference to the servicing loan. Not an FK because modules don't share DbContexts. |
| `DocumentType` | Enum: `LoanAgreement`, `AmortizationSchedule`, `PaymentReceipt`. |
| `MongoBlobId` | ObjectId of the blob in Mongo. |
| `ByteLength` | Size in bytes. |
| `ContentHash` | SHA-256 of the PDF bytes. |
| `GeneratedAt` | `Instant`. |
| `GeneratedByCorrelationId` | Guid. |
| `SchemaVersion` | Int. The PDF layout's version — increments break snapshots, documented in the module changelog. |

**Mongo (`documents.blobs`)** — one document per PDF: `{ _id, data (Binary), createdAt }`.

### Transactional guarantee (two-phase pattern)

The module uses a two-phase pattern to keep SQL and Mongo consistent:

1. Write the Mongo blob first; capture `MongoBlobId`.
2. In the SQL transaction, insert the metadata row (with `MongoBlobId`) + the outbox `DocumentGenerated` row; commit.
3. On SQL commit failure, enqueue a compensation task to delete the orphan Mongo blob (background janitor process; documented separately in M3).

This is weaker than a distributed transaction but adequate for a portfolio demo. The ADR documents the limitation and compensation story.

## Persistence — `documents` schema

| Table | Purpose |
|---|---|
| `Documents` | Metadata (as above). |
| `DocumentsOutbox` | Integration events pending dispatch. |
| `BlobCleanupQueue` | Mongo blob ids pending deletion due to failed SQL commits. |
| `CommandReceipts_GenerateLoanAgreement` | Idempotency receipts. |
| `CommandReceipts_GenerateSchedule` | " |
| `CommandReceipts_GenerateReceipt` | " |

## DTOs (public)

- `GenerateLoanAgreement { CommandId, LoanId, LoanAgreementData, CorrelationId }` — `LoanAgreementData` is a snapshot taken from Reporting at the moment of generation so the document is deterministic.
- `GenerateSchedule { CommandId, LoanId, CorrelationId }` — looks up the loan's current schedule from Reporting.
- `GenerateReceipt { CommandId, PaymentId, ReceiptData, CorrelationId }`.
- `DocumentMetadata`, `DocumentType`.
- `DocumentId : Guid`.

## Feature slices (M3)

```
Credere.Modules.Documents/Application/Commands/
├── GenerateLoanAgreement/
├── GenerateSchedule/
└── GenerateReceipt/

Credere.Modules.Documents/Application/IntegrationEventHandlers/
├── OnLoanDisbursed/
└── OnPaymentRecorded/
```

## Invariants

- A `GeneratedAt` instant is recorded with every document.
- `ContentHash` is validated on retrieval to detect blob corruption.
- Attempting to generate the same document (same `CommandId`) returns the original `DocumentId`.
- A PDF generation failure does not leave orphan Mongo blobs after the janitor runs.

## Testing

- **Snapshot tests** with Verify.Xunit on PDF text extraction (not the PDF bytes, which are non-deterministic across QuestPDF versions). A canonical loan has a locked snapshot of the extracted text across pages.
- **Integration tests** cover the two-phase pattern including crash-between-writes scenarios (simulated by throwing from the SQL commit step).
- **Property-based** on the loan-agreement schedule rendering: sum of printed principal portions equals the printed principal; APRC printed matches the computed one to four decimals.

## Open items

See [`../open-questions.md`](../open-questions.md) #5 on Mongo blobs vs MinIO object storage.
