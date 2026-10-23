using Microsoft.OpenApi;

namespace Roamly.Api.Common.Http;

/// <summary>Documenta gli header HTTP usati per la concorrenza ottimistica.</summary>
public static class ConcurrencyOpenApiExtensions
{
    private static readonly string[] EtagSuccessStatusCodes = ["200", "201", "204"];

    /// <summary>Aggiunge alla richiesta il precondizionale obbligatorio If-Match.</summary>
    public static RouteHandlerBuilder WithIfMatchOpenApi(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddOpenApiOperationTransformer((operation, _, _) =>
        {
            operation.Parameters ??= [];
            if (!operation.Parameters.Any(parameter =>
                    string.Equals(parameter.Name, "If-Match", StringComparison.OrdinalIgnoreCase)))
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "If-Match",
                    In = ParameterLocation.Header,
                    Required = true,
                    Description = "ETag forte restituito dalla lettura della risorsa, nel formato \"<base64url RowVersion>\".",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                });
            }

            return Task.CompletedTask;
        });
    }

    /// <summary>Aggiunge alle risposte di creazione o lettura l'ETag necessario agli aggiornamenti successivi.</summary>
    public static RouteHandlerBuilder WithEtagResponseOpenApi(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddOpenApiOperationTransformer((operation, _, _) =>
        {
            if (operation.Responses is { } responses)
            {
                foreach (var statusCode in EtagSuccessStatusCodes)
                {
                    if (responses.TryGetValue(statusCode, out var response)
                        && response is OpenApiResponse openApiResponse)
                    {
                        var headers = openApiResponse.Headers ?? new Dictionary<string, IOpenApiHeader>();
                        headers["ETag"] = new OpenApiHeader
                        {
                            Description = "ETag forte nel formato \"<base64url RowVersion>\" della risorsa o del suo aggregate root, da inviare nel successivo If-Match.",
                            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                        };
                        openApiResponse.Headers = headers;
                    }
                }
            }

            return Task.CompletedTask;
        });
    }

    /// <summary>Documenta l'header Location della risposta 201 di creazione.</summary>
    public static RouteHandlerBuilder WithLocationResponseOpenApi(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddOpenApiOperationTransformer((operation, _, _) =>
        {
            if (operation.Responses is { } responses
                && responses.TryGetValue("201", out var response)
                && response is OpenApiResponse createdResponse)
            {
                var headers = createdResponse.Headers ?? new Dictionary<string, IOpenApiHeader>();
                headers["Location"] = new OpenApiHeader
                {
                    Description = "URI della risorsa appena creata.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                };
                createdResponse.Headers = headers;
            }

            return Task.CompletedTask;
        });
    }
}
