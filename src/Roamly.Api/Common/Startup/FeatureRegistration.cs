using Roamly.Api.Common.Email;
using Roamly.Api.Common.Identity;
using Roamly.Api.Common.Ownership;
using Roamly.Api.Features.Authentication.ConfirmEmail;
using Roamly.Api.Features.Authentication.ChangePassword;
using Roamly.Api.Features.Authentication.Login;
using Roamly.Api.Features.Authentication.Logout;
using Roamly.Api.Features.Authentication.Register;
using Roamly.Api.Features.Authentication.ResendEmailConfirmation;
using Roamly.Api.Features.Campers.CreateCamper;
using Roamly.Api.Features.Campers.CreateMaintenance;
using Roamly.Api.Features.Campers.Equipment.CreateEquipment;
using Roamly.Api.Features.Campers.Equipment.DeleteEquipment;
using Roamly.Api.Features.Campers.Equipment.GetEquipment;
using Roamly.Api.Features.Campers.Equipment.ListEquipment;
using Roamly.Api.Features.Campers.Equipment.UpdateEquipment;
using Roamly.Api.Features.Campers.GetCamperDashboard;
using Roamly.Api.Features.Campers.GetCamperProfile;
using Roamly.Api.Features.Campers.ListMaintenance;
using Roamly.Api.Features.Campers.ListMaintenanceLogs;
using Roamly.Api.Features.Campers.ListCampers;
using Roamly.Api.Features.Campers.RecordMaintenanceLog;
using Roamly.Api.Features.Campers.UpdateCamperProfile;
using Roamly.Api.Features.Identity.GetMe;
using Roamly.Api.Features.Identity.AccountDeletion;
using Roamly.Api.Features.Identity.AccountExport;
using Roamly.Api.Features.Trips.CreateTrip;
using Roamly.Api.Features.Trips.GetTrip;
using Roamly.Api.Features.Trips.ListTrips;
using Roamly.Api.Features.Trips.TransitionTrip;
using Roamly.Api.Features.Trips.UpdateTrip;
using Roamly.Api.Features.Expenses.CreateExpense;
using Roamly.Api.Features.Expenses.GetExpense;
using Roamly.Api.Features.Expenses.ListExpenses;
using Roamly.Api.Features.Expenses.UpdateExpense;
using Roamly.Api.Features.Expenses.DeleteExpense;
using Roamly.Api.Features.Checklists.CreateChecklist;
using Roamly.Api.Features.Checklists.ConfirmChecklistSeed;
using Roamly.Api.Features.Checklists.DeleteChecklist;
using Roamly.Api.Features.Checklists.GetChecklist;
using Roamly.Api.Features.Checklists.GetChecklistItem;
using Roamly.Api.Features.Checklists.ListChecklists;
using Roamly.Api.Features.Checklists.PreviewChecklistSeed;
using Roamly.Api.Features.Checklists.UpdateChecklist;
using Roamly.Api.Features.Journal.CreateJournalEntry;
using Roamly.Api.Features.Journal;
using Roamly.Api.Features.Journal.DeleteJournalEntry;
using Roamly.Api.Features.Journal.GetJournalEntry;
using Roamly.Api.Features.Journal.GetJournalAttachment;
using Roamly.Api.Features.Journal.GetJournalAttachmentDownload;
using Roamly.Api.Features.Journal.ListJournalEntries;
using Roamly.Api.Features.Journal.UpdateJournalEntry;
using Roamly.Api.Features.Journal.UploadJournalAttachment;
using Roamly.Api.Features.Journal.DeleteJournalAttachment;
using Roamly.Common.Email;

namespace Roamly.Api.Common.Startup;

/// <summary>
/// Registrazione esplicita degli handler di ogni slice (R54, D1/@solomon): una riga per slice,
/// mai riflessione. E' la meta' "registrazione" della tautologia strutturalmente impossibile che
/// R44 verifica: se una slice esiste sotto <c>Features/</c> ma non ha una riga qui, R44 la trova
/// mancante, non R54 stesso — R54 non ha modo di sapere che cosa manca.
/// </summary>
public static class FeatureRegistration
{
    /// <summary>Registra gli handler di tutte le slice applicative.</summary>
    /// <param name="services">Collezione dei servizi.</param>
    /// <returns>La collezione, per concatenare le altre registrazioni.</returns>
    public static IServiceCollection AddFeatures(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Features/Authentication/
        services.AddScoped<RegisterHandler>();
        services.AddScoped<ConfirmEmailHandler>();
        services.AddScoped<ChangePasswordHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<ResendEmailConfirmationHandler>();

        // Features/Identity/
        services.AddScoped<GetMeHandler>();
        services.AddScoped<AccountExportHandler>();
        services.AddScoped<GetAccountDeletionPreviewHandler>();
        services.AddScoped<GetAccountDeletionStatusHandler>();
        services.AddScoped<RequestAccountDeletionHandler>();
        services.AddScoped<RevokeAccountDeletionHandler>();
        services.AddScoped<OwnedResourceWalker>();
        services.AddScoped<AccountErasureJob>();
        services.AddScoped<ProvisionUserJob>();
        services.AddSingleton<IProvisioningConsole, ProvisioningConsole>();

        // Features/Campers/
        services.AddScoped<CreateCamperHandler>();
        services.AddScoped<GetCamperDashboardHandler>();
        services.AddScoped<GetCamperProfileHandler>();
        services.AddScoped<ListCampersHandler>();
        services.AddScoped<ListMaintenanceHandler>();
        services.AddScoped<ListMaintenanceLogsHandler>();
        services.AddScoped<RecordMaintenanceLogHandler>();
        services.AddScoped<CreateMaintenanceHandler>();
        services.AddScoped<UpdateCamperProfileHandler>();
        services.AddScoped<CreateEquipmentHandler>();
        services.AddScoped<ListEquipmentHandler>();
        services.AddScoped<GetEquipmentHandler>();
        services.AddScoped<UpdateEquipmentHandler>();
        services.AddScoped<DeleteEquipmentHandler>();

        // Features/Trips/
        services.AddScoped<CreateTripHandler>();
        services.AddScoped<GetTripHandler>();
        services.AddScoped<ListTripsHandler>();
        services.AddScoped<UpdateTripHandler>();
        services.AddScoped<TransitionTripHandler>();

        // Features/Checklists/
        services.AddScoped<CreateChecklistHandler>();
        services.AddScoped<ConfirmChecklistSeedHandler>();
        services.AddScoped<GetChecklistHandler>();
        services.AddScoped<GetChecklistItemHandler>();
        services.AddScoped<ListChecklistsHandler>();
        services.AddScoped<PreviewChecklistSeedHandler>();
        services.AddScoped<UpdateChecklistHandler>();
        services.AddScoped<DeleteChecklistHandler>();
        services.AddScoped<CreateChecklistItemHandler>();
        services.AddScoped<UpdateChecklistItemHandler>();
        services.AddScoped<ReorderChecklistItemsHandler>();
        services.AddScoped<DeleteChecklistItemHandler>();

        // Features/Expenses/
        services.AddScoped<CreateExpenseHandler>();
        services.AddScoped<GetExpenseHandler>();
        services.AddScoped<ListExpensesHandler>();
        services.AddScoped<UpdateExpenseHandler>();
        services.AddScoped<DeleteExpenseHandler>();

        // Features/Journal/
        services.AddScoped<CreateJournalEntryHandler>();
        services.AddScoped<ListJournalEntriesHandler>();
        services.AddScoped<GetJournalEntryHandler>();
        services.AddScoped<UpdateJournalEntryHandler>();
        services.AddScoped<DeleteJournalEntryHandler>();
        services.AddScoped<UploadJournalAttachmentHandler>();
        services.AddScoped<GetJournalAttachmentHandler>();
        services.AddScoped<GetJournalAttachmentDownloadHandler>();
        services.AddScoped<DeleteJournalAttachmentHandler>();
        services.AddScoped<JournalAttachmentReconciliationJob>();
        services.AddScoped<JournalAttachmentDeletionOutboxJob>();
        services.AddScoped<IJournalAttachmentDeletionDrainer>(provider =>
            provider.GetRequiredService<JournalAttachmentDeletionOutboxJob>());

        // Fake di sviluppo: nessun provider SMTP e' stato scelto (OPEN-DECISIONS.md). Sostituirlo
        // e' l'unica riga da cambiare quando arriva un provider reale.
        services.AddSingleton<IEmailSender, LoggingEmailSender>();

        return services;
    }
}
