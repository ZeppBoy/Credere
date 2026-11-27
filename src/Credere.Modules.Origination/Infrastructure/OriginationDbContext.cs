using Credere.Modules.Origination.Contracts;
using Credere.Modules.Origination.Infrastructure.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace Credere.Modules.Origination.Infrastructure;

/// <summary>
/// The Origination module's own <see cref="DbContext"/>, over the <c>origination</c> schema (ADR-0003). It maps the
/// current-state table, the append-only history, the outbox and one receipt table for each command handler.
/// </summary>
internal sealed class OriginationDbContext : DbContext
{
    private const int CurrencyLength = 3;
    private const int MoneyPrecision = 38;
    private const int MoneyScale = 2;
    private const int PercentagePrecision = 38;
    private const int PercentageScale = 28;

    /// <summary>Initializes a new instance of the <see cref="OriginationDbContext"/> class.</summary>
    /// <param name="options">The options, with the connection and the migrations history table.</param>
    public OriginationDbContext(DbContextOptions<OriginationDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the current-state rows.</summary>
    public DbSet<LoanApplicationRow> LoanApplications => Set<LoanApplicationRow>();

    /// <summary>Gets the history rows.</summary>
    public DbSet<LoanApplicationEventRow> LoanApplicationEvents => Set<LoanApplicationEventRow>();

    /// <summary>Gets the outbox rows.</summary>
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    /// <summary>Gets the receipts of one command handler.</summary>
    /// <param name="handler">The handler name, one of <see cref="OriginationSchema.ReceiptHandlers"/>.</param>
    /// <returns>The set over that handler's receipt table.</returns>
    public DbSet<CommandReceipt> Receipts(string handler)
    {
        if (!OriginationSchema.ReceiptHandlers.Contains(handler, StringComparer.Ordinal))
        {
            throw new ArgumentException($"'{handler}' has no receipt table.", nameof(handler));
        }

        return Set<CommandReceipt>(OriginationSchema.ReceiptTable(handler));
    }

    /// <summary>Applies the module's explicit type mappings: NodaTime types and the typed application id.</summary>
    /// <param name="configurationBuilder">The convention builder.</param>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<Instant>().HaveConversion<InstantConverter>().HaveColumnType("datetime2(7)");
        configurationBuilder.Properties<LocalDate>().HaveConversion<LocalDateConverter>().HaveColumnType("date");
        configurationBuilder.Properties<LoanApplicationId>().HaveConversion<LoanApplicationIdConverter>();
    }

    /// <summary>Maps the tables, keys, precisions and indexes.</summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(OriginationSchema.Name);

        modelBuilder.Entity<LoanApplicationRow>(entity =>
        {
            entity.ToTable(OriginationSchema.LoanApplications);
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.FullName).IsRequired();
            entity.Property(row => row.CountryCode).HasMaxLength(2).IsFixedLength().IsUnicode(false);
            entity.Property(row => row.ProductCode).HasMaxLength(32).IsUnicode(false);
            ConfigureMoney(entity.Property(row => row.MonthlyIncomeAmount), entity.Property(row => row.MonthlyIncomeCurrency));
            ConfigureMoney(
                entity.Property(row => row.MonthlyDebtPaymentsAmount),
                entity.Property(row => row.MonthlyDebtPaymentsCurrency));
            ConfigureMoney(
                entity.Property(row => row.RequestedPrincipalAmount),
                entity.Property(row => row.RequestedPrincipalCurrency));
            ConfigureMoney(entity.Property(row => row.DecidedPrincipalAmount), entity.Property(row => row.DecidedPrincipalCurrency));
            entity.Property(row => row.DecidedNominalRate).HasPrecision(PercentagePrecision, PercentageScale);
            entity.Property(row => row.State).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
            entity.Property(row => row.LastOutcome).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
            entity.Property(row => row.RowVersion).IsRowVersion();
            entity.HasIndex(row => new { row.State, row.SubmittedAt });
        });

        modelBuilder.Entity<LoanApplicationEventRow>(entity =>
        {
            entity.ToTable(OriginationSchema.LoanApplicationEvents);
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.Type).HasMaxLength(100).IsUnicode(false).IsRequired();
            entity.Property(row => row.PayloadJson).IsRequired();
            entity.HasIndex(row => new { row.StreamId, row.StreamVersion }).IsUnique();
            entity.HasOne<LoanApplicationRow>()
                .WithMany()
                .HasForeignKey(row => row.StreamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable(OriginationSchema.Outbox);
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.Type).HasMaxLength(100).IsUnicode(false).IsRequired();
            entity.Property(row => row.PayloadJson).IsRequired();
            entity.Property(row => row.LastError).HasMaxLength(OutboxMessage.LastErrorMaxLength);
            entity.HasIndex(row => row.OccurredAt).HasFilter("[DispatchedAt] IS NULL");
        });

        foreach (var handler in OriginationSchema.ReceiptHandlers)
        {
            var table = OriginationSchema.ReceiptTable(handler);
            modelBuilder.SharedTypeEntity<CommandReceipt>(table, entity =>
            {
                entity.ToTable(table);
                entity.HasKey(row => row.CommandId);
                entity.Property(row => row.CommandId).ValueGeneratedNever();
                entity.Property(row => row.OutcomeJson).IsRequired();
            });
        }
    }

    private static void ConfigureMoney(PropertyBuilder<decimal> amount, PropertyBuilder<string> currency)
    {
        amount.HasPrecision(MoneyPrecision, MoneyScale);
        currency.HasMaxLength(CurrencyLength).IsFixedLength().IsUnicode(false);
    }

    private static void ConfigureMoney(PropertyBuilder<decimal?> amount, PropertyBuilder<string?> currency)
    {
        amount.HasPrecision(MoneyPrecision, MoneyScale);
        currency.HasMaxLength(CurrencyLength).IsFixedLength().IsUnicode(false);
    }
}
