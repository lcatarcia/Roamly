namespace Roamly.Common.Storage;

/// <summary>Confine specifico per le immagini del Journal, senza credenziali esposte al client.</summary>
public interface IJournalImageStorage
{
    /// <summary>Carica i byte su una chiave assegnata dal server.</summary>
    Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Emette un link read-only, limitato al singolo blob e con scadenza breve.</summary>
    Task<JournalImageDownloadLink?> CreateReadOnlyDownloadLinkAsync(
        string key,
        CancellationToken cancellationToken);

    /// <summary>Apre i byte originali per l'export account.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Elimina un blob se presente; ripetere l'operazione e' sicuro.</summary>
    Task DeleteIfExistsAsync(string key, CancellationToken cancellationToken);

    /// <summary>Elimina i blob sotto un prefisso owner; un completamento senza eccezioni conferma lo sweep.</summary>
    Task<long> DeleteByPrefixAsync(string ownerPrefix, CancellationToken cancellationToken);

    /// <summary>Enumera le chiavi sotto un prefisso per la riconciliazione.</summary>
    IAsyncEnumerable<string> ListKeysAsync(string prefix, CancellationToken cancellationToken);
}

/// <summary>URL SAS e istante esatto di scadenza.</summary>
/// <param name="Uri">URL firmato per il download diretto.</param>
/// <param name="ExpiresAtUtc">Istante UTC oltre il quale la firma e' rifiutata.</param>
public sealed record JournalImageDownloadLink(Uri Uri, DateTimeOffset ExpiresAtUtc);
