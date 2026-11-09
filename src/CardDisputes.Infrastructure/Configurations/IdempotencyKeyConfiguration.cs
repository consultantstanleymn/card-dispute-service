using CardDisputes.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardDisputes.Infrastructure.Configurations;

public sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("idempotency_keys");

        builder.HasKey(entry => new { entry.ClientId, entry.Key });

        builder.Property(entry => entry.ClientId)
            .HasColumnName("client_id")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(entry => entry.Key)
            .HasColumnName("key")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(entry => entry.RequestHash)
            .HasColumnName("request_hash")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(entry => entry.StatusCode)
            .HasColumnName("status_code")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(entry => entry.ResponseBody)
            .HasColumnName("response_body")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(entry => entry.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
