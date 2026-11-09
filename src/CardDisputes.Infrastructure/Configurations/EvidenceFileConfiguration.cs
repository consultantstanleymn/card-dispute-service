using CardDisputes.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardDisputes.Infrastructure.Configurations;

public sealed class EvidenceFileConfiguration : IEntityTypeConfiguration<EvidenceFile>
{
    public void Configure(EntityTypeBuilder<EvidenceFile> builder)
    {
        builder.ToTable("evidence_files");

        builder.HasKey(file => file.Id);

        builder.Property(file => file.Id)
            .HasColumnName("id")
            .HasColumnType("uuid");

        builder.Property(file => file.DisputeId)
            .HasColumnName("dispute_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(file => file.FileName)
            .HasColumnName("file_name")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(file => file.ContentType)
            .HasColumnName("content_type")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(file => file.SizeBytes)
            .HasColumnName("size_bytes")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(file => file.Sha256)
            .HasColumnName("sha256")
            .HasColumnType("char(64)")
            .IsRequired();

        builder.Property(file => file.ObjectKey)
            .HasColumnName("object_key")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(file => file.UploadedAt)
            .HasColumnName("uploaded_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasOne<DisputeRow>()
            .WithMany()
            .HasForeignKey(file => file.DisputeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(file => file.DisputeId)
            .HasDatabaseName("ix_evidence_files_dispute_id");
    }
}
