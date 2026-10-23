using Microsoft.OpenApi;

namespace Roamly.Api.Common.Http;

/// <summary>Documenta le risposte 400 che possono essere problemi generici o di validazione.</summary>
public static class ProblemDetailsOpenApiExtensions
{
    /// <summary>Descrive entrambi gli shape RFC 9457 nella risposta OpenAPI 400.</summary>
    public static RouteHandlerBuilder WithProblemDetailsOrValidationOpenApi(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddOpenApiOperationTransformer((operation, context, _) =>
        {
            if (operation.Responses is not { } responses
                || !responses.TryGetValue("400", out var response)
                || response is not OpenApiResponse { Content: { } content }
                || !content.TryGetValue("application/problem+json", out var problemResponse))
            {
                return Task.CompletedTask;
            }

            problemResponse.Schema = new OpenApiSchema
            {
                AnyOf =
                [
                    new OpenApiSchemaReference("ProblemDetails", context.Document),
                    new OpenApiSchemaReference("HttpValidationProblemDetails", context.Document),
                ],
            };
            return Task.CompletedTask;
        });
    }
}
