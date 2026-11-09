using CardDisputes.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardDisputes.Infrastructure.Configurations;

public sealed class DisputeEventConfiguration : IEntityTypeConfiguration<DisputeEvent>
{
    public void Configure(EntityTypeBuilder<DisputeEvent> builder)
    {
        builder.ToTable("dispute_events");

        builder.HasKey(disputeEvent => disputeEvent.Id);

        builder.Property(disputeEvent => disputeEvent.Id)
            .HasColumnName("id")
            .HasColumnType("uuid");

        builder.Property(disputeEvent => disputeEvent.DisputeId)
            .HasColumnName("dispute_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(disputeEvent => disputeEvent.FromStatus)
            .HasColumnName("from_status")
            .HasColumnType("text");

        builder.Property(disputeEvent => disputeEvent.ToStatus)
            .HasColumnName("to_status")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(disputeEvent => disputeEvent.ClientId)
            .HasColumnName("client_id")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(disputeEvent => disputeEvent.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasOne<DisputeRow>()
            .WithMany()
            .HasForeignKey(disputeEvent => disputeEvent.DisputeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(disputeEvent => disputeEvent.DisputeId)
            .HasDatabaseName("ix_dispute_events_dispute_id");
    }
}
