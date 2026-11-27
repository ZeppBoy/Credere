using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Credere.Modules.Servicing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialServicing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "servicing");

            migrationBuilder.CreateTable(
                name: "CommandReceipts_AccrueInterest",
                schema: "servicing",
                columns: table => new
                {
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    OutcomeJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandReceipts_AccrueInterest", x => x.CommandId);
                });

            migrationBuilder.CreateTable(
                name: "CommandReceipts_Activate",
                schema: "servicing",
                columns: table => new
                {
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    OutcomeJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandReceipts_Activate", x => x.CommandId);
                });

            migrationBuilder.CreateTable(
                name: "CommandReceipts_ApplyAging",
                schema: "servicing",
                columns: table => new
                {
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    OutcomeJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandReceipts_ApplyAging", x => x.CommandId);
                });

            migrationBuilder.CreateTable(
                name: "CommandReceipts_Disburse",
                schema: "servicing",
                columns: table => new
                {
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    OutcomeJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandReceipts_Disburse", x => x.CommandId);
                });

            migrationBuilder.CreateTable(
                name: "CommandReceipts_ExecutePayoff",
                schema: "servicing",
                columns: table => new
                {
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    OutcomeJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandReceipts_ExecutePayoff", x => x.CommandId);
                });

            migrationBuilder.CreateTable(
                name: "CommandReceipts_RecordPayment",
                schema: "servicing",
                columns: table => new
                {
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    OutcomeJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandReceipts_RecordPayment", x => x.CommandId);
                });

            migrationBuilder.CreateTable(
                name: "CommandReceipts_Recover",
                schema: "servicing",
                columns: table => new
                {
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    OutcomeJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandReceipts_Recover", x => x.CommandId);
                });

            migrationBuilder.CreateTable(
                name: "CommandReceipts_RegisterDisbursementIntent",
                schema: "servicing",
                columns: table => new
                {
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    OutcomeJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandReceipts_RegisterDisbursementIntent", x => x.CommandId);
                });

            migrationBuilder.CreateTable(
                name: "CommandReceipts_Restructure",
                schema: "servicing",
                columns: table => new
                {
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    OutcomeJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandReceipts_Restructure", x => x.CommandId);
                });

            migrationBuilder.CreateTable(
                name: "CommandReceipts_WriteOff",
                schema: "servicing",
                columns: table => new
                {
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    OutcomeJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandReceipts_WriteOff", x => x.CommandId);
                });

            migrationBuilder.CreateTable(
                name: "DisbursementIntents",
                schema: "servicing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceReference = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedTermsVersion = table.Column<int>(type: "int", nullable: false),
                    Product = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Principal = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    NominalRate = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    TermMonths = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisbursementIntents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                schema: "servicing",
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
                name: "Loans",
                schema: "servicing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisbursementIntentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceReference = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TermsVersion = table.Column<int>(type: "int", nullable: false),
                    Product = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Principal = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    RemainingPrincipal = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    FeesOutstanding = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    PastDueInterest = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    CurrentInterest = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    NominalRate = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    DayCountConvention = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    TermMonths = table.Column<int>(type: "int", nullable: false),
                    Frequency = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Repayment = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    PastDueThreshold = table.Column<int>(type: "int", nullable: false),
                    DefaultThreshold = table.Column<int>(type: "int", nullable: false),
                    Calendar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisbursementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FirstPaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AccruedThroughDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AccrualSegmentStart = table.Column<DateOnly>(type: "date", nullable: false),
                    State = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    DaysPastDue = table.Column<int>(type: "int", nullable: true),
                    StreamVersion = table.Column<long>(type: "bigint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Loans_DisbursementIntents_DisbursementIntentId",
                        column: x => x.DisbursementIntentId,
                        principalSchema: "servicing",
                        principalTable: "DisbursementIntents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccrualLedger",
                schema: "servicing",
                columns: table => new
                {
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Amount = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccrualLedger", x => new { x.LoanId, x.BusinessDate });
                    table.ForeignKey(
                        name: "FK_AccrualLedger_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "servicing",
                        principalTable: "Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoanEvents",
                schema: "servicing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StreamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StreamVersion = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    EventVersion = table.Column<int>(type: "int", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateOnly>(type: "date", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanEvents_Loans_StreamId",
                        column: x => x.StreamId,
                        principalSchema: "servicing",
                        principalTable: "Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoanSchedules",
                schema: "servicing",
                columns: table => new
                {
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    OpeningBalance = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    CapitalisedInterest = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    InterestDue = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    PrincipalDue = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    TotalPayment = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    ClosingBalance = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanSchedules", x => new { x.LoanId, x.Index });
                    table.ForeignKey(
                        name: "FK_LoanSchedules_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "servicing",
                        principalTable: "Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentLedger",
                schema: "servicing",
                columns: table => new
                {
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Amount = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    FeesAllocated = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    PastDueInterestAllocated = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    CurrentInterestAllocated = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    PrincipalAllocated = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Excess = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentLedger", x => x.PaymentId);
                    table.ForeignKey(
                        name: "FK_PaymentLedger_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "servicing",
                        principalTable: "Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementIntents_LoanId",
                schema: "servicing",
                table: "DisbursementIntents",
                column: "LoanId",
                unique: true,
                filter: "[LoanId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementIntents_SourceReference_AcceptedTermsVersion",
                schema: "servicing",
                table: "DisbursementIntents",
                columns: new[] { "SourceReference", "AcceptedTermsVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanEvents_StreamId_StreamVersion",
                schema: "servicing",
                table: "LoanEvents",
                columns: new[] { "StreamId", "StreamVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Loans_DisbursementIntentId",
                schema: "servicing",
                table: "Loans",
                column: "DisbursementIntentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Loans_State_DisbursementDate",
                schema: "servicing",
                table: "Loans",
                columns: new[] { "State", "DisbursementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_OccurredAt",
                schema: "servicing",
                table: "Outbox",
                column: "OccurredAt",
                filter: "[DispatchedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLedger_LoanId_EffectiveDate",
                schema: "servicing",
                table: "PaymentLedger",
                columns: new[] { "LoanId", "EffectiveDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccrualLedger",
                schema: "servicing");

            migrationBuilder.DropTable(
                name: "CommandReceipts_AccrueInterest",
                schema: "servicing");

            migrationBuilder.DropTable(
                name: "CommandReceipts_Activate",
                schema: "servicing");

            migrationBuilder.DropTable(
                name: "CommandReceipts_ApplyAging",
                schema: "servicing");

            migrationBuilder.DropTable(
                name: "CommandReceipts_Disburse",
                schema: "servicing");

            migrationBuilder.DropTable(
                name: "CommandReceipts_ExecutePayoff",
                schema: "servicing");

            migrationBuilder.DropTable(
                name: "CommandReceipts_RecordPayment",
                schema: "servicing");

            migrationBuilder.DropTable(
                name: "CommandReceipts_Recover",
                schema: "servicing");

            migrationBuilder.DropTable(
                name: "CommandReceipts_RegisterDisbursementIntent",
                schema: "servicing");

            migrationBuilder.DropTable(
                name: "CommandReceipts_Restructure",
                schema: "servicing");

            migrationBuilder.DropTable(
                name: "CommandReceipts_WriteOff",
                schema: "servicing");

            migrationBuilder.DropTable(
                name: "LoanEvents",
                schema: "servicing");

            migrationBuilder.DropTable(
                name: "LoanSchedules",
                schema: "servicing");

            migrationBuilder.DropTable(
                name: "Outbox",
                schema: "servicing");

            migrationBuilder.DropTable(
                name: "PaymentLedger",
                schema: "servicing");

            migrationBuilder.DropTable(
                name: "Loans",
                schema: "servicing");

            migrationBuilder.DropTable(
                name: "DisbursementIntents",
                schema: "servicing");
        }
    }
}
