using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Entities;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mappatura di <see cref="ErasureReceipt"/>. <b>Non owned</b>: nessuna PK composita, nessun
/// query filter, nessuna FK (CONTEXT.md §2.2). E' l'unica entita' del modello fuori da R1/R8.
/// </summary>
public sealed class ErasureReceiptConfiguration : IEntityTypeConfiguration<ErasureReceipt>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ErasureReceipt> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.SubjectHash).HasNvarchar(64).IsRequired();
        builder.Property(e => e.RequestedAtUtc).HasUtcTimestamp().IsRequired();
        builder.Property(e => e.ErasedAtUtc).HasUtcTimestamp().IsRequired();
        builder.Property(e => e.SchemaVersion).HasNvarchar(32).IsRequired();
        builder.Property(e => e.DeletedRowCounts).HasNvarcharMax().IsRequired();
        builder.Property(e => e.StorageSweepConfirmed).HasColumnType("bit").IsRequired();
    }
}
