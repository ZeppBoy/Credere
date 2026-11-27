using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Infrastructure.Converters;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Credere.Modules.Servicing.Infrastructure;

/// <summary>
/// The Servicing module's own <see cref="DbContext"/>, over the <c>servicing</c> schema (ADR-0003). It maps the loan
/// state, the append-only history, the disbursement intents, the schedule snapshots, the two ledgers, the outbox and
/// one receipt table for each command handler.
/// </summary>
internal sealed class ServicingDbContext : DbContext
{
    private const int CurrencyLength = 3;
    private const int EnumLength = 32;
    private const int DayCountNameLength = 32;

    /// <summary>Initializes a new instance of the <see cref="ServicingDbContext"/> class.</summary>
    /// <param name="options">The options, with the connection and the migrations history table.</param>
    public ServicingDbContext(DbContextOptions<ServicingDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the current-state rows.</summary>
    public DbSet<LoanRow> Loans => Set<LoanRow>();

    /// <summary>Gets the history rows.</summary>
    public DbSet<LoanEventRow> LoanEvents => Set<LoanEventRow>();

    /// <summary>Gets the disbursement intents.</summary>
    public DbSet<DisbursementIntentRow> DisbursementIntents => Set<DisbursementIntentRow>();

    /// <summary>Gets the schedule snapshot rows.</summary>
    public DbSet<LoanScheduleRow> LoanSchedules => Set<LoanScheduleRow>();

    /// <summary>Gets the accrual ledger.</summary>
    public DbSet<AccrualLedgerRow> AccrualLedger => Set<AccrualLedgerRow>();

    /// <summary>Gets the payment ledger.</summary>
    public DbSet<PaymentLedgerRow> PaymentLedger => Set<PaymentLedgerRow>();

    /// <summary>Gets the outbox rows.</summary>
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    /// <summary>Gets the receipts of one command handler.</summary>
    /// <param name="handler">The handler name, one of <see cref="ServicingSchema.ReceiptHandlers"/>.</param>
    /// <returns>The set over that handler's receipt table.</returns>
    public DbSet<CommandReceipt> Receipts(string handler)
    {
        if (!ServicingSchema.ReceiptHandlers.Contains(handler, StringComparer.Ordinal))
        {
            throw new ArgumentException($"'{handler}' has no receipt table.", nameof(handler));
        }

        return Set<CommandReceipt>(ServicingSchema.ReceiptTable(handler));
    }

    /// <summary>
    /// Applies the module's explicit type mappings: NodaTime types, the typed ids, and exact text for every decimal
    /// (see <see cref="DecimalTextConverter"/> for why no decimal column has a precision).
    /// </summary>
    /// <param name="configurationBuilder">The convention builder.</param>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<decimal>()
            .HaveConversion<DecimalTextConverter>()
            .HaveMaxLength(DecimalTextConverter.MaxLength)
            .AreUnicode(false);
        configurationBuilder.Properties<Instant>().HaveConversion<InstantConverter>().HaveColumnType("datetime2(7)");
        configurationBuilder.Properties<LocalDate>().HaveConversion<LocalDateConverter>().HaveColumnType("date");
        configurationBuilder.Properties<LoanId>().HaveConversion<LoanIdConverter>();
        configurationBuilder.Properties<DisbursementIntentId>().HaveConversion<DisbursementIntentIdConverter>();
        configurationBuilder.Properties<PaymentId>().HaveConversion<PaymentIdConverter>();
    }

    /// <summary>Maps the tables, keys, column sizes and indexes.</summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(ServicingSchema.Name);

        modelBuilder.Entity<DisbursementIntentRow>(entity =>
        {
            entity.ToTable(ServicingSchema.DisbursementIntents);
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.Product).IsRequired();
            entity.Property(row => row.Currency).HasMaxLength(CurrencyLength).IsFixedLength().IsUnicode(false);
            entity.Property(row => row.State).HasConversion<string>().HasMaxLength(EnumLength).IsUnicode(false);
            entity.Property(row => row.RowVersion).IsRowVersion();
            entity.HasIndex(row => new { row.SourceReference, row.AcceptedTermsVersion }).IsUnique();
            entity.HasIndex(row => row.LoanId).IsUnique().HasFilter("[LoanId] IS NOT NULL");
        });

        modelBuilder.Entity<LoanRow>(entity =>
        {
            entity.ToTable(ServicingSchema.Loans);
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.Product).IsRequired();
            entity.Property(row => row.Currency).HasMaxLength(CurrencyLength).IsFixedLength().IsUnicode(false);
            entity.Property(row => row.DayCountConvention).HasMaxLength(DayCountNameLength).IsUnicode(false);
            entity.Property(row => row.Calendar).IsRequired();
            entity.Property(row => row.Frequency).HasConversion<string>().HasMaxLength(EnumLength).IsUnicode(false);
            entity.Property(row => row.Repayment).HasConversion<string>().HasMaxLength(EnumLength).IsUnicode(false);
            entity.Property(row => row.State).HasConversion<string>().HasMaxLength(EnumLength).IsUnicode(false);
            entity.Property(row => row.RowVersion).IsRowVersion();
            entity.HasIndex(row => row.DisbursementIntentId).IsUnique();
            entity.HasIndex(row => new { row.State, row.DisbursementDate });
            entity.HasOne<DisbursementIntentRow>()
                .WithMany()
                .HasForeignKey(row => row.DisbursementIntentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LoanEventRow>(entity =>
        {
            entity.ToTable(ServicingSchema.LoanEvents);
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.Type).HasMaxLength(100).IsUnicode(false).IsRequired();
            entity.Property(row => row.PayloadJson).IsRequired();
            entity.HasIndex(row => new { row.StreamId, row.StreamVersion }).IsUnique();
            entity.HasOne<LoanRow>()
                .WithMany()
                .HasForeignKey(row => row.StreamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LoanScheduleRow>(entity =>
        {
            entity.ToTable(ServicingSchema.LoanSchedules);
            entity.HasKey(row => new { row.LoanId, row.Index });
            entity.Property(row => row.Index).ValueGeneratedNever();
            entity.Property(row => row.Currency).HasMaxLength(CurrencyLength).IsFixedLength().IsUnicode(false);
            entity.HasOne<LoanRow>()
                .WithMany()
                .HasForeignKey(row => row.LoanId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AccrualLedgerRow>(entity =>
        {
            entity.ToTable(ServicingSchema.AccrualLedger);
            entity.HasKey(row => new { row.LoanId, row.BusinessDate });
            entity.Property(row => row.Currency).HasMaxLength(CurrencyLength).IsFixedLength().IsUnicode(false);
            entity.HasOne<LoanRow>()
                .WithMany()
                .HasForeignKey(row => row.LoanId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentLedgerRow>(entity =>
        {
            entity.ToTable(ServicingSchema.PaymentLedger);
            entity.HasKey(row => row.PaymentId);
            entity.Property(row => row.PaymentId).ValueGeneratedNever();
            entity.Property(row => row.Currency).HasMaxLength(CurrencyLength).IsFixedLength().IsUnicode(false);
            entity.HasIndex(row => new { row.LoanId, row.EffectiveDate });
            entity.HasOne<LoanRow>()
                .WithMany()
                .HasForeignKey(row => row.LoanId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable(ServicingSchema.Outbox);
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.Type).HasMaxLength(100).IsUnicode(false).IsRequired();
            entity.Property(row => row.PayloadJson).IsRequired();
            entity.Property(row => row.LastError).HasMaxLength(OutboxMessage.LastErrorMaxLength);
            entity.HasIndex(row => row.OccurredAt).HasFilter("[DispatchedAt] IS NULL");
        });

        foreach (var handler in ServicingSchema.ReceiptHandlers)
        {
            var table = ServicingSchema.ReceiptTable(handler);
            modelBuilder.SharedTypeEntity<CommandReceipt>(table, entity =>
            {
                entity.ToTable(table);
                entity.HasKey(row => row.CommandId);
                entity.Property(row => row.CommandId).ValueGeneratedNever();
                entity.Property(row => row.OutcomeJson).IsRequired();
            });
        }
    }
}
