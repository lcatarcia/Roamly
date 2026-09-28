using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Time.Testing;
using Roamly.Common;

namespace Roamly.TestSupport;

/// <summary>
/// Stato condiviso da una sessione di costruzione: proprietario, generatore di id, tempo
/// <b>fissato</b>, modello EF e accesso tipizzato ai padri gia' costruiti.
/// <para>
/// Il tempo e' un <see cref="FakeTimeProvider"/> fermo su <see cref="FixedInstant"/> (R34,
/// ADR-0009): dati di test generati con l'ora reale non sono riproducibili, e un test che
/// confronta <c>CreatedAtUtc</c> diventerebbe funzione del momento in cui gira.
/// </para>
/// </summary>
public sealed class BuilderContext
{
    private readonly Dictionary<Type, object> _built = [];

    /// <summary>Costruisce il contesto con un generatore di id e un tempo fissati.</summary>
    /// <param name="ownerId">Proprietario di ogni entita' costruita. E' la prima colonna di ogni PK.</param>
    /// <param name="model">Modello EF da cui il registro deriva l'ordine topologico.</param>
    public BuilderContext(Guid ownerId, IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        OwnerId = ownerId;
        Model = model;
        Clock = new FakeTimeProvider(FixedInstant);
        Ids = new SequentialGuidGenerator(Clock);
    }

    /// <summary>
    /// Istante unico di tutta la suite. Il valore non ha significato di dominio: ha il solo
    /// requisito di essere <b>lo stesso a ogni esecuzione</b>.
    /// </summary>
    public static DateTimeOffset FixedInstant { get; } = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Proprietario di ogni entita' costruita in questa sessione.</summary>
    public Guid OwnerId { get; }

    /// <summary>Modello EF, sorgente del grafo delle FK.</summary>
    public IModel Model { get; }

    /// <summary>Sorgente del tempo, ferma su <see cref="FixedInstant"/>. Avanzabile dai test di scadenza.</summary>
    public FakeTimeProvider Clock { get; }

    /// <summary>Unica sorgente ammessa per <c>Id</c> (R41, ADR-0008).</summary>
    public IIdGenerator Ids { get; }

    /// <summary>Istante corrente come <see cref="DateTime"/> UTC, la forma persistita di <c>CreatedAtUtc</c>.</summary>
    public DateTime UtcNow => Clock.GetUtcNow().UtcDateTime;

    /// <summary>Data corrente, per i campi <c>date</c> del modello.</summary>
    public DateOnly Today => DateOnly.FromDateTime(UtcNow);

    /// <summary>
    /// Restituisce l'entita' padre gia' costruita in questa sessione. E' tipizzato di proposito:
    /// un dizionario <c>Type -&gt; object</c> grezzo esposto ai builder rimanderebbe a runtime un
    /// errore che qui e' un errore di compilazione.
    /// </summary>
    /// <typeparam name="T">Tipo dell'entita' padre.</typeparam>
    /// <returns>L'istanza costruita.</returns>
    /// <exception cref="InvalidOperationException">Il padre non e' stato costruito prima del figlio.</exception>
    public T Parent<T>()
        where T : class
    {
        if (_built.TryGetValue(typeof(T), out var parent))
        {
            return (T)parent;
        }

        throw new InvalidOperationException(
            "Nessuna istanza di " + typeof(T).Name + " e' stata costruita prima di questo builder. "
            + "Conseguenza: l'ordine topologico derivato da IModel non colloca "
            + typeof(T).Name + " prima del figlio che lo richiede, oppure il padre e' stato "
            + "costruito fuori da BuilderRegistry.BuildAll. Non aggiungere una lista di entita' "
            + "scritta a mano per rimediare (R33, ADR-0009): l'ordine si deriva dal modello.");
    }

    /// <summary>
    /// Registra un'istanza costruita, rendendola disponibile a <see cref="Parent{T}"/>.
    /// Chiamato da <see cref="BuilderRegistry"/>, oppure dal test L1 che inserisce un padre a mano.
    /// </summary>
    /// <param name="entity">Istanza costruita.</param>
    public void Register(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _built[entity.GetType()] = entity;
    }
}
