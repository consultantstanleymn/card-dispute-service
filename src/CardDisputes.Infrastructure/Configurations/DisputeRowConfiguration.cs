using CardDisputes.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardDisputes.Infrastructure.Configurations;

public sealed class DisputeRowConfiguration : IEntityTypeConfiguration<DisputeRow>
{
    public void Configure(EntityTypeBuilder<DisputeRow> builder)
    {
        builder.ToTable(
            "disputes",
            table =>
            {
                table.HasCheckConstraint("ck_disputes_amount_minor_positive", "amount_minor > 0");
                table.HasCheckConstraint(
                    "ck_disputes_reason_code",
                    "reason_code in ('FRAUD_CARD_ABSENT', 'GOODS_NOT_RECEIVED', 'DUPLICATE_CHARGE', 'CANCELLED_RECURRING', 'CREDIT_NOT_PROCESSED')");
                table.HasCheckConstraint(
                    "ck_disputes_status",
                    "status in ('OPENED', 'EVIDENCE_SUBMITTED', 'WON', 'LOST', 'WITHDRAWN')");
            });

        builder.HasKey(dispute => dispute.Id);

        builder.Property(dispute => dispute.Id)
            .HasColumnName("id")
            .HasColumnType("uuid");

        builder.Property(dispute => dispute.CardTxnRef)
            .HasColumnName("card_txn_ref")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(dispute => dispute.Merchant)
            .HasColumnName("merchant")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(dispute => dispute.AmountMinor)
            .HasColumnName("amount_minor")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(dispute => dispute.Currency)
            .HasColumnName("currency")
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(dispute => dispute.ReasonCode)
            .HasColumnName("reason_code")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(dispute => dispute.Status)
            .HasColumnName("status")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(dispute => dispute.OpenedAt)
            .HasColumnName("opened_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(dispute => dispute.RespondBy)
            .HasColumnName("respond_by")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(dispute => dispute.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(dispute => dispute.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(dispute => new { dispute.OpenedAt, dispute.Id })
            .HasDatabaseName("ix_disputes_opened_at_id")
            .IsDescending(true, true);

        builder.HasIndex(dispute => new { dispute.Status, dispute.OpenedAt, dispute.Id })
            .HasDatabaseName("ix_disputes_status_opened_at_id")
            .IsDescending(false, true, true);
    }
}
