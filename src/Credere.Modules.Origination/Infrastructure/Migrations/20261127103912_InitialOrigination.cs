using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Credere.Modules.Origination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialOrigination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "origination");

            migrationBuilder.CreateTable(
                name: "CommandReceipts_AcceptTerms",
                schema: "origination",
                columns: table => new
                {
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    OutcomeJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandReceipts_AcceptTerms", x => x.CommandId);
                });

            migrationBuilder.CreateTable(
                name: "CommandReceipts_DecideApplication",
                schema: "origination",
                columns: table => new
                {
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    OutcomeJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandReceipts_DecideApplication", x => x.CommandId);
                });

            migrationBuilder.CreateTable(
                name: "CommandReceipts_SubmitApplication",
                schema: "origination",
                columns: table => new
                {
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    OutcomeJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandReceipts_SubmitApplication", x => x.CommandId);
                });

            migrationBuilder.CreateTable(
                name: "CommandReceipts_WithdrawApplication",
                schema: "origination",
                columns: table => new
                {
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    OutcomeJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandReceipts_WithdrawApplication", x => x.CommandId);
                });

            migrationBuilder.CreateTable(
                name: "LoanApplications",
                schema: "origination",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    CountryCode = table.Column<string>(type: "char(2)", unicode: false, fixedLength: true, maxLength: 2, nullable: false),
                    MonthlyIncomeAmount = table.Column<decimal>(type: "decimal(38,2)", precision: 38, scale: 2, nullable: false),
                    MonthlyIncomeCurrency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    MonthlyDebtPaymentsAmount = table.Column<decimal>(type: "decimal(38,2)", precision: 38, scale: 2, nullable: false),
                    MonthlyDebtPaymentsCurrency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    ProductCode = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    RequestedPrincipalAmount = table.Column<decimal>(type: "decimal(38,2)", precision: 38, scale: 2, nullable: false),
                    RequestedPrincipalCurrency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    RequestedTermMonths = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    State = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    TermsVersion = table.Column<int>(type: "int", nullable: false),
                    DecidedPrincipalAmount = table.Column<decimal>(type: "decimal(38,2)", precision: 38, scale: 2, nullable: true),
                    DecidedPrincipalCurrency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: true),
                    DecidedNominalRate = table.Column<decimal>(type: "decimal(38,28)", precision: 38, scale: 28, nullable: true),
                    DecidedTermMonths = table.Column<int>(type: "int", nullable: true),
                    LastOutcome = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    LastRationale = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastDecidedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    StreamVersion = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                schema: "origination",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    Type = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    EventVersion = table.Column<int>(type: "int", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoanApplicationEvents",
                schema: "origination",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StreamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StreamVersion = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    EventVersion = table.Column<int>(type: "int", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanApplicationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanApplicationEvents_LoanApplications_StreamId",
                        column: x => x.StreamId,
                        principalSchema: "origination",
                        principalTable: "LoanApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoanApplicationEvents_StreamId_StreamVersion",
                schema: "origination",
                table: "LoanApplicationEvents",
                columns: new[] { "StreamId", "StreamVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanApplications_State_SubmittedAt",
                schema: "origination",
                table: "LoanApplications",
                columns: new[] { "State", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_OccurredAt",
                schema: "origination",
                table: "Outbox",
                column: "OccurredAt",
                filter: "[DispatchedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandReceipts_AcceptTerms",
                schema: "origination");

            migrationBuilder.DropTable(
                name: "CommandReceipts_DecideApplication",
                schema: "origination");

            migrationBuilder.DropTable(
                name: "CommandReceipts_SubmitApplication",
                schema: "origination");

            migrationBuilder.DropTable(
                name: "CommandReceipts_WithdrawApplication",
                schema: "origination");

            migrationBuilder.DropTable(
                name: "LoanApplicationEvents",
                schema: "origination");

            migrationBuilder.DropTable(
                name: "Outbox",
                schema: "origination");

            migrationBuilder.DropTable(
                name: "LoanApplications",
                schema: "origination");
        }
    }
}
