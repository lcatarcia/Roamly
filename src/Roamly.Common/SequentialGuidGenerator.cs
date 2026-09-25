using System.Buffers.Binary;

namespace Roamly.Common;

/// <summary>
/// GUID sequenziali nell'ORDINAMENTO DI CONFRONTO DI SQL SERVER (COMB).
/// SQL Server confronta uniqueidentifier dando priorita' ai byte 10-15: il
/// timestamp va scritto li', non nei primi byte. Per la stessa ragione
/// Guid.CreateVersion7() (RFC 9562: timestamp nei byte 0-5) NON e' sequenziale
/// su SQL Server — vedi ADR-0008.
/// Algoritmo allineato a Microsoft.EntityFrameworkCore.ValueGeneration.SequentialGuidValueGenerator.
/// </summary>
public sealed class SequentialGuidGenerator : IIdGenerator
{
    // ADR-0008 inizializza il contatore con DateTime.UtcNow.Ticks. Qui l'ora arriva da
    // TimeProvider perche' DateTime.UtcNow e' un simbolo bandito (R34, ADR-0009) e la build
    // fallirebbe: la sostituzione non tocca l'algoritmo, solo la sorgente del valore iniziale.
    private long _counter;

    /// <summary>Costruisce il generatore inizializzando il contatore all'istante corrente.</summary>
    /// <param name="timeProvider">Sorgente dell'ora, iniettata per testabilita' (R34).</param>
    public SequentialGuidGenerator(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _counter = timeProvider.GetUtcNow().UtcTicks;
    }

    /// <inheritdoc />
    public Guid NewId()
    {
        var guid = Guid.NewGuid();
        var counter = Interlocked.Increment(ref _counter);

        Span<byte> c = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(c, counter);

        Span<byte> b = stackalloc byte[16];
        guid.TryWriteBytes(b);

        // byte alti del contatore nei byte 10-15 = 1º criterio di sort di SQL Server
        b[8] = c[1]; b[9] = c[0];
        b[10] = c[7]; b[11] = c[6]; b[12] = c[5];
        b[13] = c[4]; b[14] = c[3]; b[15] = c[2];

        return new Guid(b);
    }
}
