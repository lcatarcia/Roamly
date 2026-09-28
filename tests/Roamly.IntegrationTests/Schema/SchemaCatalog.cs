using System.Data;
using Microsoft.Data.SqlClient;

namespace Roamly.IntegrationTests.Schema;

/// <summary>Una colonna di una foreign key, nell'ordine dichiarato nella clausola.</summary>
/// <param name="ConstraintName">Nome del vincolo.</param>
/// <param name="ParentTable">Tabella figlia.</param>
/// <param name="ReferencedTable">Tabella padre.</param>
/// <param name="Ordinal">Posizione della colonna nella clausola (1-based).</param>
/// <param name="ParentColumn">Colonna figlia.</param>
/// <param name="ReferencedColumn">Colonna padre referenziata.</param>
/// <param name="ReferencedColumnId">Identificatore fisico della colonna padre (<c>referenced_column_id</c>).</param>
/// <param name="DeleteAction">Azione referenziale in cancellazione.</param>
public sealed record ForeignKeyColumn(
    string ConstraintName,
    string ParentTable,
    string ReferencedTable,
    int Ordinal,
    string ParentColumn,
    string ReferencedColumn,
    int ReferencedColumnId,
    string DeleteAction);

/// <summary>Una colonna di chiave primaria, nell'ordine della chiave.</summary>
/// <param name="TableName">Tabella.</param>
/// <param name="ColumnName">Colonna.</param>
/// <param name="KeyOrdinal">Posizione nella chiave (1-based).</param>
/// <param name="IsClustered">Se l'indice della PK e' CLUSTERED.</param>
public sealed record PrimaryKeyColumn(string TableName, string ColumnName, int KeyOrdinal, bool IsClustered);

/// <summary>Il tipo fisico di una colonna, come il motore lo ha davvero creato.</summary>
/// <param name="TableName">Tabella.</param>
/// <param name="ColumnName">Colonna.</param>
/// <param name="TypeName">Nome del tipo SQL.</param>
/// <param name="Precision">Precisione.</param>
/// <param name="Scale">Scala.</param>
public sealed record ColumnType(string TableName, string ColumnName, string TypeName, int Precision, int Scale);

/// <summary>
/// Lettura dei cataloghi di sistema del database <b>reale</b>.
/// <para>
/// 🔴 Tutto cio' che sta qui interroga <c>sys.*</c>, mai <c>IModel</c>: il senso del test B e' che
/// il motore puo' smentire il modello, e un'asserzione che rilegge il modello sarebbe verde per
/// costruzione (TESTING.md §5).
/// </para>
/// </summary>
public static class SchemaCatalog
{
    /// <summary>Tabelle di Identity e infrastruttura, fuori dal perimetro delle regole di ADR-0008.</summary>
    public static bool IsRoamlyTable(string table) =>
        !table.StartsWith("AspNet", StringComparison.Ordinal)
        && !string.Equals(table, "__EFMigrationsHistory", StringComparison.Ordinal);

    /// <summary>Legge tutte le colonne di tutte le foreign key dello schema <c>dbo</c>.</summary>
    /// <param name="connectionString">Database da interrogare.</param>
    /// <returns>Le colonne, in ordine di vincolo e di posizione.</returns>
    public static Task<IReadOnlyList<ForeignKeyColumn>> ForeignKeyColumnsAsync(string connectionString) =>
        QueryAsync(
            connectionString,
            """
            SELECT  fk.name                              AS ConstraintName,
                    OBJECT_NAME(fk.parent_object_id)     AS ParentTable,
                    OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
                    fkc.constraint_column_id             AS Ordinal,
                    pc.name                              AS ParentColumn,
                    rc.name                              AS ReferencedColumn,
                    fkc.referenced_column_id             AS ReferencedColumnId,
                    fk.delete_referential_action_desc    AS DeleteAction
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
            JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
            ORDER BY fk.name, fkc.constraint_column_id;
            """,
            reader => new ForeignKeyColumn(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6),
                reader.GetString(7)));

    /// <summary>Legge le colonne di tutte le chiavi primarie, con il tipo dell'indice.</summary>
    /// <param name="connectionString">Database da interrogare.</param>
    /// <returns>Le colonne, in ordine di tabella e di chiave.</returns>
    public static Task<IReadOnlyList<PrimaryKeyColumn>> PrimaryKeyColumnsAsync(string connectionString) =>
        QueryAsync(
            connectionString,
            """
            SELECT  OBJECT_NAME(i.object_id) AS TableName,
                    c.name                   AS ColumnName,
                    ic.key_ordinal           AS KeyOrdinal,
                    CASE i.type WHEN 1 THEN 1 ELSE 0 END AS IsClustered
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = i.object_id AND c.column_id = ic.column_id
            WHERE i.is_primary_key = 1 AND ic.is_included_column = 0
            ORDER BY TableName, ic.key_ordinal;
            """,
            reader => new PrimaryKeyColumn(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetByte(2),
                reader.GetInt32(3) == 1));

    /// <summary>Legge i tipi fisici delle colonne dello schema <c>dbo</c>.</summary>
    /// <param name="connectionString">Database da interrogare.</param>
    /// <returns>I tipi, tabella per tabella.</returns>
    public static Task<IReadOnlyList<ColumnType>> ColumnTypesAsync(string connectionString) =>
        QueryAsync(
            connectionString,
            """
            SELECT  t.name  AS TableName,
                    c.name  AS ColumnName,
                    TYPE_NAME(c.user_type_id) AS TypeName,
                    c.precision,
                    c.scale
            FROM sys.columns c
            JOIN sys.tables t ON t.object_id = c.object_id
            ORDER BY t.name, c.column_id;
            """,
            reader => new ColumnType(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetByte(3),
                reader.GetByte(4)));

    /// <summary>Legge i vincoli di unicita' dichiarati (chiavi alternate).</summary>
    /// <param name="connectionString">Database da interrogare.</param>
    /// <returns>Coppie tabella/vincolo.</returns>
    public static Task<IReadOnlyList<(string Table, string Constraint)>> UniqueConstraintsAsync(string connectionString) =>
        QueryAsync(
            connectionString,
            """
            SELECT OBJECT_NAME(kc.parent_object_id) AS TableName, kc.name AS ConstraintName
            FROM sys.key_constraints kc
            WHERE kc.type = 'UQ'
            ORDER BY TableName, ConstraintName;
            """,
            reader => (reader.GetString(0), reader.GetString(1)));

    private static async Task<IReadOnlyList<T>> QueryAsync<T>(
        string connectionString,
        string sql,
        Func<IDataRecord, T> map)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

        var rows = new List<T>();
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            rows.Add(map(reader));
        }

        return rows;
    }
}
