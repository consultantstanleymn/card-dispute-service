using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardDisputes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "disputes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_txn_ref = table.Column<string>(type: "text", nullable: false),
                    merchant = table.Column<string>(type: "text", nullable: false),
                    amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false),
                    reason_code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    respond_by = table.Column<DateOnly>(type: "date", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disputes", x => x.id);
                    table.CheckConstraint("ck_disputes_amount_minor_positive", "amount_minor > 0");
                    table.CheckConstraint("ck_disputes_reason_code", "reason_code in ('FRAUD_CARD_ABSENT', 'GOODS_NOT_RECEIVED', 'DUPLICATE_CHARGE', 'CANCELLED_RECURRING', 'CREDIT_NOT_PROCESSED')");
                    table.CheckConstraint("ck_disputes_status", "status in ('OPENED', 'EVIDENCE_SUBMITTED', 'WON', 'LOST', 'WITHDRAWN')");
                });

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                columns: table => new
                {
                    client_id = table.Column<string>(type: "text", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    request_hash = table.Column<string>(type: "text", nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    response_body = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_keys", x => new { x.client_id, x.key });
                });

            migrationBuilder.CreateTable(
                name: "dispute_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "text", nullable: true),
                    to_status = table.Column<string>(type: "text", nullable: false),
                    client_id = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispute_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispute_events_disputes_dispute_id",
                        column: x => x.dispute_id,
                        principalTable: "disputes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evidence_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "char(64)", nullable: false),
                    object_key = table.Column<string>(type: "text", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_files", x => x.id);
                    table.ForeignKey(
                        name: "FK_evidence_files_disputes_dispute_id",
                        column: x => x.dispute_id,
                        principalTable: "disputes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dispute_events_dispute_id",
                table: "dispute_events",
                column: "dispute_id");

            migrationBuilder.CreateIndex(
                name: "ix_disputes_opened_at_id",
                table: "disputes",
                columns: new[] { "opened_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_disputes_status_opened_at_id",
                table: "disputes",
                columns: new[] { "status", "opened_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_evidence_files_dispute_id",
                table: "evidence_files",
                column: "dispute_id");

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION dispute_events_reject_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'dispute_events is append-only';
                END;
                $$;

                CREATE TRIGGER dispute_events_append_only
                BEFORE UPDATE OR DELETE ON dispute_events
                FOR EACH ROW
                EXECUTE FUNCTION dispute_events_reject_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS dispute_events_append_only ON dispute_events;
                DROP FUNCTION IF EXISTS dispute_events_reject_mutation();
                """);

            migrationBuilder.DropTable(
                name: "dispute_events");

            migrationBuilder.DropTable(
                name: "evidence_files");

            migrationBuilder.DropTable(
                name: "idempotency_keys");

            migrationBuilder.DropTable(
                name: "disputes");
        }
    }
}
