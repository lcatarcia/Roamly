using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roamly.Infrastructure.Identity;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>
/// Colonne aggiunte ad <c>AspNetUsers</c> da CONTEXT.md §2.2 e ADR-0004.
/// Non e' una delle 14 entita' del manifest: e' la radice di ownership, non un'entita' owned.
/// </summary>
public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Property(e => e.DisplayName).HasNvarchar(80);
        builder.Property(e => e.ReportingCurrency).HasIsoCurrency().HasDefaultValue("EUR").IsRequired();
        builder.Property(e => e.CreatedAtUtc).HasUtcTimestamp().IsRequired();
        builder.Property(e => e.DeletionRequestedAtUtc).HasUtcTimestamp();
        builder.Property(e => e.DeletionScheduledForUtc).HasUtcTimestamp();
        builder.Property(e => e.PrivacyPolicyVersionSeen).HasNvarchar(32);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_AspNetUsers_ReportingCurrency",
            "[ReportingCurrency] = UPPER([ReportingCurrency]) AND LEN([ReportingCurrency]) = 3"));

        // Indice filtrato: il job di erasure cerca solo le richieste attive, che sono poche
        // righe su tutta la tabella (ADR-0004).
        builder.HasIndex(e => e.DeletionScheduledForUtc)
            .HasFilter("[DeletionScheduledForUtc] IS NOT NULL");
    }
}
