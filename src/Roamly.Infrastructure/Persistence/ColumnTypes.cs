using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Roamly.Infrastructure.Persistence;

/// <summary>
/// Scorciatoie per i tipi di colonna dichiarati nelle schede entita' di CONTEXT.md.
/// Esistono per rendere il tipo <b>esplicito e uniforme</b>: un tipo lasciato alla convenzione
/// e' una decisione presa da qualcun altro.
/// </summary>
internal static class ColumnTypes
{
    /// <summary>Colonna <c>nvarchar(n)</c> con la stessa lunghezza anche come vincolo applicativo.</summary>
    public static PropertyBuilder<T> HasNvarchar<T>(this PropertyBuilder<T> property, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(property);

        return property
            .HasMaxLength(maxLength)
            .IsUnicode(true)
            .HasColumnType("nvarchar(" + maxLength.ToString(CultureInfo.InvariantCulture) + ")");
    }

    /// <summary>Colonna <c>nvarchar(max)</c>: testo libero senza limite di dominio.</summary>
    public static PropertyBuilder<T> HasNvarcharMax<T>(this PropertyBuilder<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return property.IsUnicode(true).HasColumnType("nvarchar(max)");
    }

    /// <summary>Codice ISO 4217: <c>char(3)</c>, non unicode.</summary>
    public static PropertyBuilder<T> HasIsoCurrency<T>(this PropertyBuilder<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return property.HasMaxLength(3).IsUnicode(false).HasColumnType("char(3)");
    }

    /// <summary>Enum a backing <see cref="byte"/> mappato su <c>tinyint</c>.</summary>
    public static PropertyBuilder<T> HasTinyint<T>(this PropertyBuilder<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return property.HasColumnType("tinyint");
    }

    /// <summary>Data pura, senza ora: <c>date</c>.</summary>
    public static PropertyBuilder<T> HasDate<T>(this PropertyBuilder<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return property.HasColumnType("date");
    }

    /// <summary>Istante UTC alla precisione dei millisecondi: <c>datetime2(3)</c>.</summary>
    public static PropertyBuilder<T> HasUtcTimestamp<T>(this PropertyBuilder<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return property.HasColumnType("datetime2(3)");
    }

    /// <summary>Colonna <c>decimal(p,s)</c> esplicita.</summary>
    public static PropertyBuilder<T> HasDecimal<T>(this PropertyBuilder<T> property, int precision, int scale)
    {
        ArgumentNullException.ThrowIfNull(property);
        return property.HasColumnType(DecimalType(precision, scale));
    }

    /// <summary>Codice ISO 4217 dentro un complex type.</summary>
    public static ComplexTypePropertyBuilder<T> HasIsoCurrency<T>(this ComplexTypePropertyBuilder<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return property.HasMaxLength(3).IsUnicode(false).HasColumnType("char(3)");
    }

    /// <summary>Colonna <c>decimal(p,s)</c> dentro un complex type.</summary>
    public static ComplexTypePropertyBuilder<T> HasDecimal<T>(
        this ComplexTypePropertyBuilder<T> property,
        int precision,
        int scale)
    {
        ArgumentNullException.ThrowIfNull(property);
        return property.HasColumnType(DecimalType(precision, scale));
    }

    private static string DecimalType(int precision, int scale) =>
        "decimal("
        + precision.ToString(CultureInfo.InvariantCulture)
        + ","
        + scale.ToString(CultureInfo.InvariantCulture)
        + ")";
}
