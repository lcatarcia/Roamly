using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Roamly.Common;
using Roamly.Infrastructure.Identity;

namespace Roamly.Infrastructure.Persistence;

/// <summary>
/// Base comune ai due <see cref="DbContext"/> del progetto. Esiste per una sola ragione tecnica:
/// il query filter "OwnerScope" deve leggere l'utente corrente <b>dall'istanza del contesto</b>.
/// EF Core sostituisce i riferimenti al <see cref="DbContext"/> con il contesto in esecuzione,
/// mentre una closure su un oggetto qualunque verrebbe catturata una volta sola nel modello
/// e restituirebbe per sempre il primo utente visto.
/// </summary>
public abstract class RoamlyDbContextBase : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ICurrentUser _currentUser;

    /// <summary>Costruisce il contesto con l'utente corrente iniettato.</summary>
    /// <param name="options">Opzioni del contesto.</param>
    /// <param name="currentUser">Utente corrente, sorgente del filtro "OwnerScope".</param>
    protected RoamlyDbContextBase(DbContextOptions options, ICurrentUser currentUser)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        _currentUser = currentUser;
    }

    /// <summary>Identificativo dell'utente corrente, letto dal query filter nominato "OwnerScope".</summary>
    public Guid CurrentUserId => _currentUser.Id;
}
