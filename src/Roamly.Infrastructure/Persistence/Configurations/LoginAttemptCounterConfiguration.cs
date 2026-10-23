using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Roamly.Infrastructure.Persistence.Configurations;

/// <summary>Mappatura dei contatori di login, non-owned e senza relazioni con Identity.</summary>
public sealed class LoginAttemptCounterConfiguration : IEntityTypeConfiguration<LoginAttemptCounter>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LoginAttemptCounter> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("LoginAttemptCounters");
        builder.HasKey(counter => new { counter.BucketKey, counter.WindowStartUtc });
        builder.Property(counter => counter.BucketKey).HasColumnType("char(64)").IsRequired();
        builder.Property(counter => counter.CounterKind)
            .HasColumnType("varchar(24)")
            .HasDefaultValue("legacy")
            .IsRequired();
        builder.Property(counter => counter.WindowStartUtc).HasUtcTimestamp().IsRequired();
        builder.Property(counter => counter.AttemptCount).HasColumnType("int").IsRequired();
        builder.Property(counter => counter.ExpiresAtUtc).HasUtcTimestamp().IsRequired();
        builder.HasIndex(counter => counter.ExpiresAtUtc);
        builder.HasIndex(counter => new { counter.CounterKind, counter.ExpiresAtUtc });
    }
}
