using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Entities;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>Mappatura di <see cref="ChecklistItem"/> (CONTEXT.md §2.3, FK #15).</summary>
/// <param name="context">Contesto da cui il query filter legge l'utente corrente.</param>
public sealed class ChecklistItemConfiguration(RoamlyDbContextBase context)
    : IEntityTypeConfiguration<ChecklistItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ChecklistItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        OwnedResourceConvention.ConfigureOwnedResource(builder, context);

        builder.Property(e => e.Text).HasNvarchar(200).IsRequired();
        builder.Property(e => e.IsDone).HasColumnType("bit").IsRequired();
        builder.Property(e => e.SortOrder).HasColumnType("int").IsRequired();

        builder.HasOne(e => e.Checklist)
            .WithMany(c => c.Items)
            .HasForeignKey(e => new { e.OwnerId, e.ChecklistId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
