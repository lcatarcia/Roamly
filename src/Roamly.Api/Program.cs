using Roamly.Api.Common.Http;
using Roamly.Api.Common.Security;
using Roamly.Api.Common.Startup;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;

static void SetRequiredProperties(OpenApiSchema schema, params string[] propertyNames)
{
    schema.Required = propertyNames.ToHashSet(StringComparer.Ordinal);
}

static void MakePropertyNonNullable(OpenApiSchema schema)
{
    if (schema.Type is { } type)
    {
        schema.Type = type & ~JsonSchemaType.Null;
    }

    if (schema.OneOf is not { } alternatives)
    {
        return;
    }

    for (var index = alternatives.Count - 1; index >= 0; index--)
    {
        if (alternatives[index] is OpenApiSchema alternative
            && alternative.Type == JsonSchemaType.Null)
        {
            alternatives.RemoveAt(index);
        }
    }
}

static bool HasViteFingerprint(PathString requestPath)
{
    var fileName = Path.GetFileName(requestPath.Value ?? string.Empty);
    var extensionIndex = fileName.LastIndexOf('.');
    var fingerprintStart = extensionIndex - 8;
    if (fingerprintStart <= 0
        || fileName[fingerprintStart - 1] is not ('-' or '.'))
    {
        return false;
    }

    for (var index = fingerprintStart; index < extensionIndex; index++)
    {
        var character = fileName[index];
        if (!char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))
        {
            return false;
        }
    }

    return true;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi("v1", options =>
{
    options.ShouldInclude = description =>
        description.RelativePath is { } path
        && (path.StartsWith("api/v1/", StringComparison.Ordinal)
            || string.Equals(path, "api/v1", StringComparison.Ordinal));
    options.AddDocumentTransformer((document, _, _) =>
    {
        if (document.Components?.Schemas is { } schemas
            && schemas.TryGetValue("TripStatus", out var schema)
            && schema is OpenApiSchema tripStatusSchema)
        {
            tripStatusSchema.Enum =
            [
                JsonValue.Create(0),
                JsonValue.Create(1),
                JsonValue.Create(2),
                JsonValue.Create(3),
            ];
        }

        if (document.Components?.Schemas is { } expenseSchemas)
        {
            SetExpenseRequestContracts(expenseSchemas);
            SetExpenseMoneyContracts(expenseSchemas);
            SetTripFuelPriceContracts(expenseSchemas);

            if (expenseSchemas.TryGetValue("ExpenseCategory", out var category)
                && category is OpenApiSchema expenseCategory)
            {
                expenseCategory.Enum =
                [
                    JsonValue.Create(0),
                    JsonValue.Create(1),
                    JsonValue.Create(2),
                    JsonValue.Create(3),
                    JsonValue.Create(4),
                    JsonValue.Create(5),
                    JsonValue.Create(6),
                ];
                expenseCategory.Description =
                    "Categorie ammesse: 0 Fuel, 1 Toll, 2 Campsite, 3 Insurance, 4 Gas, 5 Parking, 6 Other.";
            }
        }

        if (document.Components?.Schemas is { } checklistSchemas)
        {
            SetChecklistRequestContracts(checklistSchemas);
            if (checklistSchemas.TryGetValue("ChecklistKind", out var kind)
                && kind is OpenApiSchema checklistKind)
            {
                checklistKind.Enum =
                [
                    JsonValue.Create(0),
                    JsonValue.Create(1),
                    JsonValue.Create(2),
                ];
                checklistKind.Description = "Momenti ammessi: 0 Departure, 1 Arrival, 2 Storage.";
            }
        }

        return Task.CompletedTask;
    });
});

static void SetExpenseRequestContracts(IDictionary<string, IOpenApiSchema> schemas)
{
    SetRequestContract(
        schemas,
        "CreateExpenseRequest",
        ["id", "amount", "category", "incurredOnUtc"],
        ["id", "amount", "category", "incurredOnUtc"]);
    SetRequestContract(
        schemas,
        "UpdateExpenseRequest",
        ["amount", "category", "incurredOnUtc"],
        ["amount", "category", "incurredOnUtc"]);
    SetRequestContract(
        schemas,
        "ExpenseMoneyRequest",
        ["amount", "currency"],
        ["amount", "currency"]);
}

static void SetExpenseMoneyContracts(IDictionary<string, IOpenApiSchema> schemas)
{
    SetMoneyContract(schemas, "ExpenseMoneyRequest", response: false);
    SetMoneyContract(schemas, "Money", response: true);
}

static void SetTripFuelPriceContracts(IDictionary<string, IOpenApiSchema> schemas)
{
    SetRequestContract(
        schemas,
        "UpdateTripFuelPriceRequest",
        ["amount", "currency"],
        ["amount", "currency"]);
    SetMoneyContract(schemas, "UpdateTripFuelPriceRequest", response: false, positive: true);
}

static void SetChecklistRequestContracts(IDictionary<string, IOpenApiSchema> schemas)
{
    SetRequestContract(schemas, "CreateChecklistRequest", ["id", "name", "kind"], ["id", "name", "kind"]);
    SetRequestContract(schemas, "UpdateChecklistRequest", ["name", "kind"], ["name", "kind"]);
    SetRequestContract(
        schemas,
        "CreateChecklistItemRequest",
        ["id", "text"],
        ["id", "text"]);
    SetRequestContract(
        schemas,
        "UpdateChecklistItemRequest",
        ["text", "isDone"],
        ["text", "isDone"]);
    SetRequestContract(schemas, "ReorderChecklistItemsRequest", ["itemIds"], ["itemIds"]);
}

static void SetMoneyContract(
    IDictionary<string, IOpenApiSchema> schemas,
    string schemaName,
    bool response,
    bool positive = false)
{
    if (!schemas.TryGetValue(schemaName, out var candidate)
        || candidate is not OpenApiSchema schema
        || schema.Properties is not { } properties)
    {
        return;
    }

    if (response)
    {
        SetRequiredProperties(schema, "amount", "currency");
        schema.Description =
            "Importo con valuta esplicita. Complex type EF Core 10 (CONTEXT.md §2.1): mai un decimal nudo.";
    }

    if (properties.TryGetValue("amount", out var amount)
        && amount is OpenApiSchema amountSchema)
    {
        amountSchema.Type = JsonSchemaType.Number;
        amountSchema.Pattern = null;
        amountSchema.Minimum = positive ? "0.0001" : "-999999999999999.9999";
        amountSchema.Maximum = "999999999999999.9999";
        amountSchema.MultipleOf = 0.0001m;
        amountSchema.Not = new OpenApiSchema { Enum = [JsonValue.Create(0)] };
        amountSchema.Description = response
            ? "Importo, persistito come `decimal(19,4)`."
            : positive
                ? "Prezzo per litro positivo, da 0.0001 a 999999999999999.9999, con al massimo 4 decimali."
                : "Importo diverso da zero, da -999999999999999.9999 a 999999999999999.9999, con al massimo 4 decimali.";
        if (properties.TryGetValue("currency", out var currencyForDescription)
            && currencyForDescription is OpenApiSchema currencyDescription)
        {
            currencyDescription.Description = response
                ? "Codice ISO 4217, persistito come `char(3)`."
                : "Codice valuta composto da 3 lettere ASCII maiuscole.";
        }

        MakePropertyNonNullable(amountSchema);
    }

    if (properties.TryGetValue("currency", out var currency)
        && currency is OpenApiSchema currencySchema)
    {
        currencySchema.Pattern = "^[A-Z]{3}$";
        MakePropertyNonNullable(currencySchema);
    }
}

static void SetRequestContract(
    IDictionary<string, IOpenApiSchema> schemas,
    string schemaName,
    string[] requiredProperties,
    string[] nonNullableProperties)
{
    if (!schemas.TryGetValue(schemaName, out var candidate)
        || candidate is not OpenApiSchema schema
        || schema.Properties is not { } properties)
    {
        return;
    }

    SetRequiredProperties(schema, requiredProperties);
    foreach (var propertyName in nonNullableProperties)
    {
        if (properties.TryGetValue(propertyName, out var property)
            && property is OpenApiSchema propertySchema)
        {
            MakePropertyNonNullable(propertySchema);
        }
    }
}

builder.Services.AddRoamly(builder.Configuration);
builder.Services.AddJournalAttachmentDeletionBackgroundService(args);

await using var app = builder.Build();

app.UseExceptionHandler();

app.UseHttpsRedirection();

var webRootPath = app.Environment.WebRootPath;
var spaIndexPath = webRootPath is null ? null : Path.Combine(webRootPath, "index.html");
if (spaIndexPath is not null && File.Exists(spaIndexPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = context =>
        {
            var path = context.Context.Request.Path;
            if (path.Equals("/index.html", StringComparison.OrdinalIgnoreCase))
            {
                context.Context.Response.Headers.CacheControl = "no-store";
            }
            else if (path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase)
                && HasViteFingerprint(path))
            {
                context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
            }
        },
    });
}

app.UseCors(SecurityRegistration.CorsPolicyName);

app.UseAuthentication();
app.UseMiddleware<AccountSessionAuthorizationMiddleware>();
app.UseAuthorization();

app.UseMiddleware<CsrfProtectionMiddleware>();

// Health check fuori da /api/v1, anonimi, esclusi da R45 (R62): non e' contratto pubblico.
app.MapHealthChecks("/health/live", new()
{
    // Nessun check esterno: il processo e' vivo se riesce a servire questa richiesta.
    Predicate = _ => false,
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains(ObservabilityRegistration.ReadyTag),
}).AllowAnonymous();

app.MapRoamlyEndpoints();

if (spaIndexPath is not null && File.Exists(spaIndexPath))
{
    app.MapFallback(async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await Results.Problem(ProblemFactory.Create(
                context,
                StatusCodes.Status404NotFound,
                "Risorsa non trovata.",
                "https://roamly.dev/problems/not-found"))
                .ExecuteAsync(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.SendFileAsync(spaIndexPath);
    }).AllowAnonymous();
}

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
}

if (await OneShotJobDispatcher.RunIfRequestedAsync(args, app.Services, CancellationToken.None))
{
    return;
}

app.Run();

/// <summary>Punto d'ingresso, reso parziale per essere visibile a <c>WebApplicationFactory</c> (R44).</summary>
public partial class Program;
