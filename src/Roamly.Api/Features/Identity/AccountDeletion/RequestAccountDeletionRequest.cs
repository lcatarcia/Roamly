using System.ComponentModel.DataAnnotations;

namespace Roamly.Api.Features.Identity.AccountDeletion;

/// <summary>Richiesta di conferma della cancellazione dell'account.</summary>
public sealed record RequestAccountDeletionRequest([property: Required] string Password);
