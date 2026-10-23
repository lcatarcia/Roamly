using System.Text.Json.Nodes;

namespace Roamly.IntegrationTests.Conventions;

/// <summary>Verifica che il documento OpenAPI candidato estenda il contratto senza restringerlo.</summary>
internal static class OpenApiContractApprover
{
    private static readonly HashSet<string> HttpMethods =
    [
        "get", "put", "post", "delete", "options", "head", "patch", "trace",
    ];

    public static IReadOnlyList<string> FindBreakingChanges(JsonNode baseline, JsonNode candidate)
    {
        var changes = new List<string>();
        var baselinePaths = baseline["paths"] as JsonObject;
        var candidatePaths = candidate["paths"] as JsonObject;

        if (baselinePaths is null || candidatePaths is null)
        {
            return ["OpenAPI paths mancanti o non validi."];
        }

        foreach (var (path, baselinePathNode) in baselinePaths)
        {
            var baselinePath = baselinePathNode as JsonObject;
            var candidatePath = candidatePaths[path] as JsonObject;
            if (candidatePath is null)
            {
                changes.Add($"Route rimossa: {path}.");
                continue;
            }

            CompareParameters(
                baselinePath?["parameters"] as JsonArray,
                candidatePath["parameters"] as JsonArray,
                baseline,
                candidate,
                path,
                changes);

            foreach (var (method, baselineOperationNode) in baselinePath ?? [])
            {
                if (!HttpMethods.Contains(method))
                {
                    continue;
                }

                var location = $"{method.ToUpperInvariant()} {path}";
                var baselineOperation = baselineOperationNode as JsonObject;
                var candidateOperation = candidatePath[method] as JsonObject;
                if (candidateOperation is null)
                {
                    changes.Add($"Operation rimossa: {location}.");
                    continue;
                }

                CompareOperation(
                    baselineOperation!,
                    candidateOperation,
                    baseline,
                    candidate,
                    location,
                    changes);
            }
        }

        return changes;
    }

    private static void CompareOperation(
        JsonObject baseline,
        JsonObject candidate,
        JsonNode baselineDocument,
        JsonNode candidateDocument,
        string location,
        List<string> changes)
    {
        var baselineOperationId = baseline["operationId"]?.GetValue<string>();
        var candidateOperationId = candidate["operationId"]?.GetValue<string>();
        if (baselineOperationId is not null && baselineOperationId != candidateOperationId)
        {
            changes.Add($"OperationId rinominato in {location}: {baselineOperationId}.");
        }

        CompareParameters(
            baseline["parameters"] as JsonArray,
            candidate["parameters"] as JsonArray,
            baselineDocument,
            candidateDocument,
            location,
            changes);

        var baselineRequestBody = baseline["requestBody"] as JsonObject;
        var candidateRequestBody = candidate["requestBody"] as JsonObject;
        if (baselineRequestBody is not null && candidateRequestBody is null)
        {
            changes.Add($"Request body rimosso in {location}.");
        }
        else if (baselineRequestBody is not null && candidateRequestBody is not null)
        {
            if (baselineRequestBody["required"]?.GetValue<bool>() != true
                && candidateRequestBody["required"]?.GetValue<bool>() == true)
            {
                changes.Add($"Request body opzionale reso obbligatorio in {location}.");
            }

            CompareContent(
                baselineRequestBody["content"] as JsonObject,
                candidateRequestBody["content"] as JsonObject,
                baselineDocument,
                candidateDocument,
                location + " request",
                SchemaDirection.Request,
                changes);
        }

        var baselineResponses = baseline["responses"] as JsonObject;
        var candidateResponses = candidate["responses"] as JsonObject;
        if (baselineResponses is null || candidateResponses is null)
        {
            changes.Add($"Responses mancanti o non validi in {location}.");
            return;
        }

        foreach (var (status, baselineResponseNode) in baselineResponses)
        {
            var candidateResponse = candidateResponses[status] as JsonObject;
            if (candidateResponse is null)
            {
                changes.Add($"Risposta {status} rimossa da {location}.");
                continue;
            }

            CompareContent(
                (baselineResponseNode as JsonObject)?["content"] as JsonObject,
                candidateResponse["content"] as JsonObject,
                baselineDocument,
                candidateDocument,
                $"{location} response {status}",
                SchemaDirection.Response,
                changes);
        }
    }

    private static void CompareParameters(
        JsonArray? baseline,
        JsonArray? candidate,
        JsonNode baselineDocument,
        JsonNode candidateDocument,
        string location,
        List<string> changes)
    {
        var baselineParameters = ResolveParameters(baseline, baselineDocument, location, changes);
        var candidateParameters = ResolveParameters(candidate, candidateDocument, location, changes);

        foreach (var (key, oldParameter) in baselineParameters)
        {
            if (!candidateParameters.TryGetValue(key, out var newParameter))
            {
                changes.Add($"Parametro rimosso o rinominato in {location}: {key}.");
                continue;
            }

            if (oldParameter["required"]?.GetValue<bool>() != true
                && newParameter["required"]?.GetValue<bool>() == true)
            {
                changes.Add($"Parametro opzionale reso obbligatorio in {location}: {key}.");
            }

            CompareSchema(
                oldParameter["schema"],
                newParameter["schema"],
                baselineDocument,
                candidateDocument,
                $"{location} parameter {key}",
                SchemaDirection.Request,
                changes,
                []);

            CompareContent(
                oldParameter["content"] as JsonObject,
                newParameter["content"] as JsonObject,
                baselineDocument,
                candidateDocument,
                $"{location} parameter {key}",
                SchemaDirection.Request,
                changes);
        }

        foreach (var (key, newParameter) in candidateParameters)
        {
            if (!baselineParameters.ContainsKey(key)
                && newParameter["required"]?.GetValue<bool>() == true)
            {
                changes.Add($"Parametro obbligatorio aggiunto in {location}: {key}.");
            }
        }
    }

    private static Dictionary<string, JsonObject> ResolveParameters(
        JsonArray? parameters,
        JsonNode document,
        string location,
        List<string> changes)
    {
        var resolvedParameters = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var parameter in (parameters ?? []).OfType<JsonObject>())
        {
            var reference = parameter["$ref"]?.GetValue<string>();
            var resolved = reference is null
                ? parameter
                : ResolveReference(reference, document) as JsonObject;
            if (resolved is null)
            {
                changes.Add($"Parametro non risolvibile in {location}: {reference}.");
                continue;
            }

            var key = ParameterKey(resolved);
            if (!resolvedParameters.TryAdd(key, resolved))
            {
                changes.Add($"Parametro duplicato in {location}: {key}.");
            }
        }

        return resolvedParameters;
    }

    private static string ParameterKey(JsonObject parameter) =>
        $"{parameter["in"]?.GetValue<string>() ?? "?"}:{parameter["name"]?.GetValue<string>() ?? "?"}";

    private static void CompareContent(
        JsonObject? baseline,
        JsonObject? candidate,
        JsonNode baselineDocument,
        JsonNode candidateDocument,
        string location,
        SchemaDirection direction,
        List<string> changes)
    {
        if (baseline is null)
        {
            return;
        }

        if (candidate is null)
        {
            changes.Add($"Content rimosso da {location}.");
            return;
        }

        foreach (var (mediaType, baselineMediaNode) in baseline)
        {
            var candidateMedia = candidate[mediaType] as JsonObject;
            if (candidateMedia is null)
            {
                changes.Add($"Media type {mediaType} rimosso da {location}.");
                continue;
            }

            CompareSchema(
                baselineMediaNode?["schema"],
                candidateMedia["schema"],
                baselineDocument,
                candidateDocument,
                $"{location} {mediaType}",
                direction,
                changes,
                []);
        }
    }

    private static void CompareSchema(
        JsonNode? baseline,
        JsonNode? candidate,
        JsonNode baselineDocument,
        JsonNode candidateDocument,
        string location,
        SchemaDirection direction,
        List<string> changes,
        HashSet<string> visitedReferences)
    {
        if (baseline is null)
        {
            return;
        }

        if (candidate is null)
        {
            changes.Add($"Schema rimosso da {location}.");
            return;
        }

        if (baseline is not JsonObject oldSchema || candidate is not JsonObject newSchema)
        {
            if (!JsonNode.DeepEquals(baseline, candidate))
            {
                changes.Add($"Schema ristretto o cambiato in {location}.");
            }

            return;
        }

        var oldReference = oldSchema["$ref"]?.GetValue<string>();
        var newReference = newSchema["$ref"]?.GetValue<string>();
        if (oldReference is not null || newReference is not null)
        {
            var referencePair = $"{oldReference ?? "(inline)"} -> {newReference ?? "(inline)"}";
            if (!visitedReferences.Add(referencePair))
            {
                return;
            }

            var resolvedOld = ResolveReference(oldReference, baselineDocument) ?? oldSchema;
            var resolvedNew = ResolveReference(newReference, candidateDocument) ?? newSchema;
            CompareSchema(
                resolvedOld,
                resolvedNew,
                baselineDocument,
                candidateDocument,
                location,
                direction,
                changes,
                visitedReferences);
        }

        CompareTypes(oldSchema, newSchema, location, changes);
        CompareEnums(oldSchema, newSchema, location, changes);
        CompareFormatsAndPatterns(oldSchema, newSchema, location, changes);
        CompareBounds(oldSchema, newSchema, location, changes);
        CompareCompositeConstraints(oldSchema, newSchema, direction, location, changes);

        foreach (var keyword in new[] { "uniqueItems", "additionalProperties" })
        {
            CompareRestrictionKeyword(
                oldSchema,
                newSchema,
                keyword,
                baselineDocument,
                candidateDocument,
                location,
                direction,
                changes,
                visitedReferences);
        }

        var oldProperties = oldSchema["properties"] as JsonObject;
        if (oldProperties is not null)
        {
            var newProperties = newSchema["properties"] as JsonObject;
            foreach (var (name, oldProperty) in oldProperties)
            {
                var propertyLocation = $"{location}.{name}";
                if (newProperties is null || !newProperties.TryGetPropertyValue(name, out var newProperty))
                {
                    changes.Add($"Proprietà rimossa o rinominata: {propertyLocation}.");
                    continue;
                }

                CompareSchema(
                    oldProperty,
                    newProperty,
                    baselineDocument,
                    candidateDocument,
                    propertyLocation,
                    direction,
                    changes,
                    visitedReferences);
            }

            CompareRequiredProperties(oldSchema, newSchema, location, direction, changes);
        }

        if (oldSchema["items"] is JsonNode oldItems)
        {
            CompareSchema(
                oldItems,
                newSchema["items"],
                baselineDocument,
                candidateDocument,
                location + "[]",
                direction,
                changes,
                visitedReferences);
        }
        else if (newSchema["items"] is not null)
        {
            changes.Add($"items ristretto in {location}.");
        }
    }

    private static void CompareTypes(
        JsonObject baseline,
        JsonObject candidate,
        string location,
        List<string> changes)
    {
        var oldTypes = ReadTypes(baseline["type"]);
        var newTypes = ReadTypes(candidate["type"]);
        if (oldTypes is null || newTypes is null)
        {
            if (oldTypes is null && newTypes is not null)
            {
                changes.Add($"Tipo ristretto in {location}.");
            }

            return;
        }

        if (oldTypes.Any(oldType => !newTypes.Any(newType => IsSupertype(newType, oldType))))
        {
            changes.Add($"Tipo ristretto in {location}: {string.Join("|", oldTypes)}.");
        }
    }

    private static HashSet<string>? ReadTypes(JsonNode? typeNode)
    {
        if (typeNode is null)
        {
            return null;
        }

        if (typeNode is JsonArray types)
        {
            return types.Select(type => type?.GetValue<string>() ?? string.Empty)
                .ToHashSet(StringComparer.Ordinal);
        }

        return [typeNode.GetValue<string>()];
    }

    private static bool IsSupertype(string candidate, string baseline) =>
        candidate == baseline || candidate == "number" && baseline == "integer";

    private static void CompareEnums(
        JsonObject baseline,
        JsonObject candidate,
        string location,
        List<string> changes)
    {
        var oldEnum = baseline["enum"] as JsonArray;
        var newEnum = candidate["enum"] as JsonArray;
        if (oldEnum is null)
        {
            if (newEnum is not null)
            {
                changes.Add($"Enum ristretto in {location}.");
            }

            return;
        }

        if (newEnum is not null && oldEnum.Any(oldValue =>
                !newEnum.Any(newValue => JsonNode.DeepEquals(oldValue, newValue))))
        {
            changes.Add($"Valori rimossi dall'enum in {location}.");
        }

        if (baseline["const"] is JsonNode oldConstant
            && candidate["const"] is JsonNode newConstant
            && !JsonNode.DeepEquals(oldConstant, newConstant))
        {
            changes.Add($"Const ristretto in {location}.");
        }
        else if (baseline["const"] is null && candidate["const"] is not null)
        {
            changes.Add($"Const aggiunto in {location}.");
        }
    }

    private static void CompareFormatsAndPatterns(
        JsonObject baseline,
        JsonObject candidate,
        string location,
        List<string> changes)
    {
        foreach (var keyword in new[] { "format", "pattern" })
        {
            var oldValue = baseline[keyword]?.GetValue<string>();
            var newValue = candidate[keyword]?.GetValue<string>();
            if (newValue is not null && oldValue != newValue)
            {
                changes.Add($"{keyword} ristretto o cambiato in {location}.");
            }
        }
    }

    private static void CompareBounds(
        JsonObject baseline,
        JsonObject candidate,
        string location,
        List<string> changes)
    {
        CompareNumericBound(baseline, candidate, "minimum", "exclusiveMinimum", lower: true, location, changes);
        CompareNumericBound(baseline, candidate, "maximum", "exclusiveMaximum", lower: false, location, changes);

        foreach (var (minimumKey, maximumKey) in new[]
        {
            ("minLength", "maxLength"),
            ("minItems", "maxItems"),
            ("minProperties", "maxProperties"),
        })
        {
            CompareIntegerBound(baseline, candidate, minimumKey, lower: true, location, changes);
            CompareIntegerBound(baseline, candidate, maximumKey, lower: false, location, changes);
        }
    }

    private static void CompareCompositeConstraints(
        JsonObject baseline,
        JsonObject candidate,
        SchemaDirection direction,
        string location,
        List<string> changes)
    {
        foreach (var keyword in new[]
        {
            "allOf", "anyOf", "oneOf", "not", "if", "then", "else",
            "contains", "minContains", "maxContains", "propertyNames",
            "patternProperties", "dependentRequired", "dependentSchemas",
            "prefixItems", "unevaluatedProperties", "unevaluatedItems",
            "multipleOf", "contentEncoding", "contentMediaType",
        })
        {
            if (candidate[keyword] is JsonNode newConstraint
                && !JsonNode.DeepEquals(baseline[keyword], newConstraint))
            {
                changes.Add($"Vincolo {keyword} ristretto o cambiato in {location}.");
            }
        }

        var restrictionKeyword = direction == SchemaDirection.Request ? "readOnly" : "writeOnly";
        if (candidate[restrictionKeyword]?.GetValue<bool>() == true
            && baseline[restrictionKeyword]?.GetValue<bool>() != true)
        {
            changes.Add($"Vincolo readOnly/writeOnly aggiunto in {location}.");
        }
    }

    private static void CompareNumericBound(
        JsonObject baseline,
        JsonObject candidate,
        string inclusiveKeyword,
        string exclusiveKeyword,
        bool lower,
        string location,
        List<string> changes)
    {
        var oldBound = ReadBound(baseline, inclusiveKeyword, exclusiveKeyword);
        var newBound = ReadBound(candidate, inclusiveKeyword, exclusiveKeyword);
        if (newBound is null)
        {
            return;
        }

        if (oldBound is null
            || (lower
                ? newBound.Value.Value > oldBound.Value.Value
                    || newBound.Value.Value == oldBound.Value.Value
                    && newBound.Value.Exclusive && !oldBound.Value.Exclusive
                : newBound.Value.Value < oldBound.Value.Value
                    || newBound.Value.Value == oldBound.Value.Value
                    && newBound.Value.Exclusive && !oldBound.Value.Exclusive))
        {
            changes.Add($"Vincolo {inclusiveKeyword}/{exclusiveKeyword} ristretto in {location}.");
        }
    }

    private static (decimal Value, bool Exclusive)? ReadBound(
        JsonObject schema,
        string inclusiveKeyword,
        string exclusiveKeyword)
    {
        if (schema[exclusiveKeyword] is JsonValue exclusiveValue
            && exclusiveValue.TryGetValue<decimal>(out var exclusiveNumber))
        {
            return (exclusiveNumber, true);
        }

        if (schema[inclusiveKeyword] is JsonValue inclusiveValue
            && inclusiveValue.TryGetValue<decimal>(out var inclusiveNumber))
        {
            return (inclusiveNumber, false);
        }

        return null;
    }

    private static void CompareIntegerBound(
        JsonObject baseline,
        JsonObject candidate,
        string keyword,
        bool lower,
        string location,
        List<string> changes)
    {
        if (candidate[keyword] is not JsonValue newValue
            || !newValue.TryGetValue<int>(out var newBound))
        {
            return;
        }

        if (baseline[keyword] is not JsonValue oldValue
            || !oldValue.TryGetValue<int>(out var oldBound)
            || (lower ? newBound > oldBound : newBound < oldBound))
        {
            changes.Add($"Vincolo {keyword} ristretto in {location}.");
        }
    }

    private static void CompareRestrictionKeyword(
        JsonObject baseline,
        JsonObject candidate,
        string keyword,
        JsonNode baselineDocument,
        JsonNode candidateDocument,
        string location,
        SchemaDirection direction,
        List<string> changes,
        HashSet<string> visitedReferences)
    {
        var oldValue = baseline[keyword];
        var newValue = candidate[keyword];
        if (JsonNode.DeepEquals(oldValue, newValue))
        {
            return;
        }

        if (keyword == "uniqueItems")
        {
            if (newValue?.GetValue<bool>() == true && oldValue?.GetValue<bool>() != true)
            {
                changes.Add($"uniqueItems ristretto in {location}.");
            }

            return;
        }

        if (newValue is JsonValue newBoolean && newBoolean.TryGetValue<bool>(out var allowsAdditional))
        {
            if (!allowsAdditional && oldValue?.GetValue<bool>() != false)
            {
                changes.Add($"additionalProperties ristretto in {location}.");
            }

            return;
        }

        if (newValue is JsonObject || newValue is JsonArray)
        {
            CompareSchema(
                oldValue ?? JsonValue.Create(true),
                newValue,
                baselineDocument,
                candidateDocument,
                location + ".additionalProperties",
                direction,
                changes,
                visitedReferences);
        }
    }

    private static void CompareRequiredProperties(
        JsonObject baseline,
        JsonObject candidate,
        string location,
        SchemaDirection direction,
        List<string> changes)
    {
        var oldRequired = (baseline["required"] as JsonArray ?? [])
            .Select(name => name?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);
        var newRequired = (candidate["required"] as JsonArray ?? [])
            .Select(name => name?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);

        var breaking = direction == SchemaDirection.Request
            ? newRequired.Except(oldRequired, StringComparer.Ordinal)
            : oldRequired.Except(newRequired, StringComparer.Ordinal);

        foreach (var name in breaking)
        {
            changes.Add(direction == SchemaDirection.Request
                ? $"Proprietà request resa obbligatoria in {location}: {name}."
                : $"Proprietà response non più garantita in {location}: {name}.");
        }
    }

    private static JsonNode? ResolveReference(string? reference, JsonNode document)
    {
        if (reference is null || !reference.StartsWith("#/", StringComparison.Ordinal))
        {
            return null;
        }

        JsonNode? current = document;
        foreach (var segment in reference[2..].Split('/'))
        {
            var key = segment.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
            current = current is JsonObject obj ? obj[key] : null;
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private enum SchemaDirection
    {
        Request,
        Response,
    }
}
