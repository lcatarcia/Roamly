using System.Reflection;
using System.Text.Json;
using System.IO.Pipelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Roamly.Domain.Abstractions;

namespace Roamly.Api.Common.Ownership;

/// <summary>
/// Walker runtime-driven delle entità owned. Mantiene nello stesso seam la selezione R8, i
/// conteggi e la proiezione JSON: Identity, relazioni e tabelle non-owned non entrano nell'export.
/// </summary>
public sealed class OwnedResourceWalker
{
    private const int FlushThreshold = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly MethodInfo CountMethod = typeof(OwnedResourceWalker)
        .GetMethod(nameof(CountOwnedAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo QueryMethod = typeof(OwnedResourceWalker)
        .GetMethod(nameof(QueryOwnedTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Deriva i tipi esportabili dal modello EF runtime, escludendo Identity e i non-owned.</summary>
    public static IReadOnlyList<IEntityType> GetEntityTypes(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model.GetEntityTypes()
            .Where(entityType => typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType))
            .OrderBy(entityType => entityType.ClrType.Name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Conta in sequenza i record owned del proprietario, lasciando attivo il query filter.</summary>
    public async Task<IReadOnlyList<OwnedResourceCount>> CountAsync(
        DbContext db,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var counts = new List<OwnedResourceCount>();
        foreach (var entityType in GetEntityTypes(db.Model))
        {
            var count = await CountEntityAsync(db, entityType, ownerId, cancellationToken).ConfigureAwait(false);
            counts.Add(new OwnedResourceCount(entityType.ClrType.Name, count));
        }

        return counts;
    }

    /// <summary>Scrive un array JSON in streaming, senza materializzare l'insieme di record.</summary>
    public async Task<long> WriteJsonArrayAsync(
        DbContext db,
        IEntityType entityType,
        Guid ownerId,
        Stream destination,
        PipeWriter responseWriter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(responseWriter);
        EnsureOwnedTypeIsInModel(db.Model, entityType);

        using var writer = new Utf8JsonWriter(destination);
        writer.WriteStartArray();
        var count = await WriteEntitiesAsync(
            db, entityType, ownerId, destination, writer, responseWriter, cancellationToken).ConfigureAwait(false);
        writer.WriteEndArray();
        await FlushAsync(writer, destination, responseWriter, cancellationToken).ConfigureAwait(false);
        return count;
    }

    private static async Task<long> CountEntityAsync(
        DbContext db,
        IEntityType entityType,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var method = CountMethod.MakeGenericMethod(entityType.ClrType);
        var countTask = (Task<long>)method.Invoke(null, [db, ownerId, cancellationToken])!;
        return await countTask.ConfigureAwait(false);
    }

    private static Task<long> CountOwnedAsync<TEntity>(
        DbContext db,
        Guid ownerId,
        CancellationToken cancellationToken)
        where TEntity : class =>
        db.Set<TEntity>()
            .AsNoTracking()
            .Where(entity => EF.Property<Guid>(entity, nameof(IOwnedResource.OwnerId)) == ownerId)
            .LongCountAsync(cancellationToken);

    private static IAsyncEnumerable<object> QueryOwned(
        DbContext db,
        Type entityClrType,
        Guid ownerId)
    {
        var method = QueryMethod.MakeGenericMethod(entityClrType);
        return (IAsyncEnumerable<object>)method.Invoke(null, [db, ownerId])!;
    }

    private static IAsyncEnumerable<TEntity> QueryOwnedTyped<TEntity>(DbContext db, Guid ownerId)
        where TEntity : class =>
        db.Set<TEntity>()
            .AsNoTracking()
            .Where(entity => EF.Property<Guid>(entity, nameof(IOwnedResource.OwnerId)) == ownerId)
            .AsAsyncEnumerable();

    private static async Task<long> WriteEntitiesAsync(
        DbContext db,
        IEntityType entityType,
        Guid ownerId,
        Stream destination,
        Utf8JsonWriter writer,
        PipeWriter responseWriter,
        CancellationToken cancellationToken)
    {
        long count = 0;
        await foreach (var entity in QueryOwned(db, entityType.ClrType, ownerId)
            .WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            WriteEntity(writer, entityType, entity, db);
            count++;
            if (writer.BytesPending >= FlushThreshold)
            {
                await FlushAsync(writer, destination, responseWriter, cancellationToken).ConfigureAwait(false);
            }
        }

        return count;
    }

    private static void WriteEntity(
        Utf8JsonWriter writer,
        ITypeBase type,
        object instance,
        DbContext db)
    {
        writer.WriteStartObject();
        foreach (var property in type.GetProperties().OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            writer.WritePropertyName(JsonOptions.PropertyNamingPolicy!.ConvertName(property.Name));
            JsonSerializer.Serialize(writer, ReadPropertyValue(db, instance, property), property.ClrType, JsonOptions);
        }

        foreach (var property in type.GetComplexProperties().OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            writer.WritePropertyName(JsonOptions.PropertyNamingPolicy!.ConvertName(property.Name));
            WriteComplexValue(writer, property.ComplexType, ReadComplexValue(instance, property), db);
        }

        writer.WriteEndObject();
    }

    private static void WriteComplexValue(
        Utf8JsonWriter writer,
        IComplexType complexType,
        object? value,
        DbContext db)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        WriteEntity(writer, complexType, value, db);
    }

    private static object? ReadPropertyValue(DbContext db, object instance, IProperty property)
    {
        if (property.PropertyInfo is { } propertyInfo)
        {
            return propertyInfo.GetValue(instance);
        }

        if (property.FieldInfo is { } fieldInfo)
        {
            return fieldInfo.GetValue(instance);
        }

        return db.Entry(instance).Property(property.Name).CurrentValue;
    }

    private static object? ReadComplexValue(object instance, IComplexProperty property) =>
        property.PropertyInfo?.GetValue(instance) ?? property.FieldInfo?.GetValue(instance);

    private static async Task FlushAsync(
        Utf8JsonWriter writer,
        Stream destination,
        PipeWriter responseWriter,
        CancellationToken cancellationToken)
    {
        writer.Flush();
        destination.Flush();
        var result = await responseWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsCanceled || result.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new IOException("La risposta di export e' stata interrotta durante lo streaming.");
        }
    }

    private static void EnsureOwnedTypeIsInModel(IModel model, IEntityType entityType)
    {
        if (!typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType)
            || model.FindEntityType(entityType.ClrType) is null)
        {
            throw new InvalidOperationException(
                $"Il tipo {entityType.ClrType.Name} non e' un'entita' owned del modello runtime.");
        }
    }
}

/// <summary>Conteggio informativo di un tipo owned.</summary>
/// <param name="EntityType">Nome del tipo EF.</param>
/// <param name="Count">Numero di record visibili al proprietario.</param>
public sealed record OwnedResourceCount(string EntityType, long Count);
