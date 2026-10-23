namespace Roamly.Api.Common.Security;

/// <summary>Claim che identifica una sessione autenticata limitata alla revoca della cancellazione.</summary>
public static class AccountSessionClaims
{
    /// <summary>Tipo del claim di modalità sessione.</summary>
    public const string ModeClaimType = "roamly:session-mode";

    /// <summary>Valore del claim per una sessione limitata.</summary>
    public const string DeletionLimited = "deletion-limited";
}

/// <summary>Consente a un endpoint protetto di essere usato da una sessione limitata.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class AllowRestrictedAccountSessionAttribute : Attribute
{
}

/// <summary>Consente l'accesso a un endpoint quando la password iniziale deve ancora essere cambiata.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class AllowPasswordChangeSessionAttribute : Attribute
{
}
