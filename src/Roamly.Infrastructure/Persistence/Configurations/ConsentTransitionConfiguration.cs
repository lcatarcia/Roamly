using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Entities;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>Registro owned senza proiezione duplicata o dati di rete.</summary>
public sealed class ConsentTransitionConfiguration(RoamlyDbContextBase context)
    : IEntityTypeConfiguration<ConsentTransition>
{
    public void Configure(EntityTypeBuilder<ConsentTransition> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        OwnedResourceConvention.ConfigureOwnedResource(builder, context);
        builder.Property(e => e.Purpose).HasNvarchar(64).IsRequired();
        builder.Property(e => e.Version).HasNvarchar(64).IsRequired();
        builder.Property(e => e.TextReference).HasNvarchar(256).IsRequired();
        builder.Property(e => e.Action).HasNvarchar(6).IsRequired();
        builder.HasIndex(e => new { e.OwnerId, e.Purpose, e.Revision }).IsUnique();
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_ConsentTransition_Revision", "[Revision] > 0");
            table.HasCheckConstraint("CK_ConsentTransition_Action", "[Action] IN (N'grant', N'revoke')");
        });
    }
}
