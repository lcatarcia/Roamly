using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Domain.Entities;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>Mappatura di <see cref="JournalEntry"/> (CONTEXT.md §2.3, FK #16 e #17).</summary>
/// <param name="context">Contesto da cui il query filter legge l'utente corrente.</param>
public sealed class JournalEntryConfiguration(RoamlyDbContextBase context)
    : IEntityTypeConfiguration<JournalEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        OwnedResourceConvention.ConfigureOwnedResource(builder, context);

        builder.Property(e => e.EntryOnUtc).HasDate().IsRequired();
        builder.Property(e => e.Title).HasNvarchar(120);
        builder.Property(e => e.Body).HasNvarcharMax().IsRequired();

        builder.HasOne(e => e.Trip)
            .WithMany(t => t.JournalEntries)
            .HasForeignKey(e => new { e.OwnerId, e.TripId })
            .OnDelete(DeleteBehavior.Cascade);

        // 🔴 NO ACTION per obbligo, non per stile: Trip -> JournalEntry diretto e
        // Trip -> TripStop -> JournalEntry sono due percorsi verso la stessa tabella.
        // Con entrambi in cascade la migration fallisce con l'errore 1785 (CONTEXT.md §5.2 caso c).
        builder.HasOne(e => e.TripStop)
            .WithMany()
            .HasForeignKey(e => new { e.OwnerId, e.TripStopId })
            .OnDelete(DeleteBehavior.NoAction);
    }
}
