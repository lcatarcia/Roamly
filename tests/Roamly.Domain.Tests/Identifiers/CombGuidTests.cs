using System.Data.SqlTypes;
using Microsoft.Extensions.Time.Testing;
using Roamly.Common;

namespace Roamly.Domain.Tests.Identifiers;

/// <summary>
/// Monotonicita' del COMB di <see cref="SequentialGuidGenerator"/> (ADR-0008).
/// <para>
/// Il confronto usa <see cref="SqlGuid"/>, che implementa l'ordinamento di SQL Server per
/// <c>uniqueidentifier</c>: i byte <b>10-15</b> sono il primo criterio, poi 8-9, poi gli altri.
/// E' la ragione per cui il timestamp va scritto in coda e per cui <c>Guid.CreateVersion7()</c>
/// (RFC 9562, timestamp nei byte 0-5) <b>non</b> e' sequenziale su SQL Server.
/// </para>
/// <para>
/// Il tempo e' iniettato con <c>FakeTimeProvider</c> (R34): con l'ora reale questo test
/// dipenderebbe dall'istante di esecuzione e non potrebbe confrontare due generatori.
/// </para>
/// </summary>
public sealed class CombGuidTests
{
    private static readonly DateTimeOffset Origin = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Consecutive_identifiers_increase_in_sql_server_sort_order()
    {
        var generator = new SequentialGuidGenerator(new FakeTimeProvider(Origin));

        var previous = new SqlGuid(generator.NewId());
        for (var i = 0; i < 1_000; i++)
        {
            var current = new SqlGuid(generator.NewId());

            Assert.True(
                current.CompareTo(previous) > 0,
                $"Id numero {i + 1} non e' successivo al precedente nell'ordinamento di SQL Server: "
                + $"{previous.Value} -> {current.Value}. Conseguenza: gli INSERT non cadono in coda "
                + "all'indice clusterizzato e tornano frammentazione e page split (ADR-0008).");

            previous = current;
        }
    }

    [Fact]
    public void The_timestamp_is_written_into_the_bytes_sql_server_sorts_first()
    {
        var generator = new SequentialGuidGenerator(new FakeTimeProvider(Origin));

        // Byte 10-15: primo criterio di ordinamento. Due id generati a distanza di un tick di
        // contatore condividono questi byte; e' il segmento 8-9 a muoversi. Se il timestamp
        // finisse nei byte 0-5 (forma UUIDv7) questi sei byte sarebbero casuali e diversi.
        var first = generator.NewId().ToByteArray();
        var second = generator.NewId().ToByteArray();

        Assert.True(
            first.AsSpan(10, 6).SequenceEqual(second.AsSpan(10, 6)),
            "I byte 10-15 di due id consecutivi divergono: il contatore non e' scritto dove SQL Server "
            + "ordina per primo, e la sequenzialita' e' solo apparente.");

        Assert.True(
            new SqlGuid(new Guid(second)).CompareTo(new SqlGuid(new Guid(first))) > 0,
            "Il secondo id non segue il primo nell'ordinamento di SQL Server.");
    }

    [Fact]
    public void A_generator_seeded_later_produces_greater_identifiers()
    {
        var early = new SequentialGuidGenerator(new FakeTimeProvider(Origin));
        var late = new SequentialGuidGenerator(new FakeTimeProvider(Origin.AddDays(1)));

        var earlyId = new SqlGuid(early.NewId());
        var lateId = new SqlGuid(late.NewId());

        Assert.True(
            lateId.CompareTo(earlyId) > 0,
            "Un generatore inizializzato piu' tardi non produce id maggiori: il contatore non e' "
            + "ancorato all'ora, e due processi concorrenti scriverebbero in punti arbitrari dell'indice.");
    }
}
