using Roamly.Domain.Entities;

namespace Roamly.Api.Features.Journal;

/// <summary>Contratto pubblico della voce Journal e dei metadati delle immagini.</summary>
public sealed record JournalEntryResponse(
    Guid Id,
    Guid TripId,
    Guid? TripStopId,
    DateOnly EntryOnUtc,
    string? Title,
    string Body,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<JournalAttachmentResponse> Attachments)
{
    public static JournalEntryResponse From(JournalEntry entry) => new(
        entry.Id,
        entry.TripId,
        entry.TripStopId,
        entry.EntryOnUtc,
        entry.Title,
        entry.Body,
        entry.CreatedAtUtc,
        entry.UpdatedAtUtc,
        entry.Attachments
            .OrderBy(attachment => attachment.Id)
            .Select(JournalAttachmentResponse.From)
            .ToArray());
}

/// <summary>Metadati pubblici, non identificativi del client, di una foto originale.</summary>
public sealed record JournalAttachmentResponse(
    Guid Id,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAtUtc)
{
    public static JournalAttachmentResponse From(JournalEntryAttachment attachment) => new(
        attachment.Id,
        attachment.ContentType,
        attachment.SizeBytes,
        attachment.CreatedAtUtc);
}
