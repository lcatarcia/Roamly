using System.Text.Json;
using System.Text.Json.Nodes;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Conventions;

/// <summary>R51: il contratto OpenAPI committato puo' crescere, ma non restringersi.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class R51_OpenApiContractApprovalTests(SqlServerFixture sqlServer)
{
    private const string BaselineContract = """
        {
          "openapi": "3.1.0",
          "paths": {
            "/api/v1/items": {
              "get": {
                "operationId": "ListItems",
                "parameters": [
                  {
                    "name": "limit",
                    "in": "query",
                    "required": false,
                    "schema": { "type": "integer" }
                  }
                ],
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": {
                      "schema": {
                        "type": "object",
                        "properties": {
                          "id": { "type": "string" },
                          "filter": { "type": "string" }
                        },
                        "required": ["id"]
                      }
                    }
                  }
                },
                "responses": {
                  "200": {
                    "description": "Success",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "properties": {
                            "id": { "type": "string" },
                            "name": { "type": "string" }
                          },
                          "required": ["id", "name"]
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    [Fact]
    public async Task served_api_contract_is_backward_compatible_with_the_committed_snapshot()
    {
        using var factory = new RoamlyApiFactory(sqlServer.ConnectionString);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            "/openapi/v1.json",
            TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var generated = JsonNode.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
        var repositoryRoot = FindRepositoryRoot();
        var snapshotPath = Path.Combine(
            repositoryRoot,
            "tests",
            "Roamly.IntegrationTests",
            "Conventions",
            "openapi-v1.json");
        var generatedPath = Path.Combine(repositoryRoot, "TestResults", "openapi-v1.generated.json");
        Directory.CreateDirectory(Path.GetDirectoryName(generatedPath)!);
        await File.WriteAllTextAsync(
            generatedPath,
            generated.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
            TestContext.Current.CancellationToken);

        AssertExpenseContracts(generated);
        AssertChecklistContracts(generated);
        AssertTripCollectionContract(generated);
        AssertTripUpdateConcurrencyContract(generated);
        AssertTripTransitionContract(generated);
        AssertBadRequestSchemaIncludesBothProblemShapes(generated, "/api/v1/campers");
        AssertBadRequestSchemaIncludesBothProblemShapes(
            generated,
            "/api/v1/campers/{camperId}/maintenance");
        AssertBadRequestSchemaIncludesBothProblemShapes(
            generated,
            "/api/v1/campers/{camperId}/maintenance/{maintenanceItemId}/logs");
        AssertBadRequestSchemaIncludesBothProblemShapes(
            generated,
            "/api/v1/campers/{camperId}/expenses");

        Assert.True(File.Exists(snapshotPath), "Snapshot R51 mancante: " + snapshotPath);
        var snapshot = JsonNode.Parse(File.ReadAllText(snapshotPath))!;

        var changes = OpenApiContractApprover.FindBreakingChanges(snapshot, generated);

        Assert.True(
            changes.Count == 0,
            "Il contratto /api/v1 si restringe rispetto allo snapshot R51: "
            + string.Join("; ", changes));
        Assert.True(
            JsonNode.DeepEquals(snapshot, generated),
            "Il documento OpenAPI e' cambiato. Revisionare il diff e aggiornare "
            + "tests/Roamly.IntegrationTests/Conventions/openapi-v1.json con il risultato generato.");
    }

    [Fact]
    public void new_routes_and_optional_properties_are_approved()
    {
        var baseline = ParseBaseline();
        var candidate = ParseBaseline();

        candidate["paths"]!["/api/v1/items"]!["get"]!["responses"]!["200"]!["content"]!
            ["application/json"]!["schema"]!["properties"]!["description"] =
            new JsonObject { ["type"] = "string" };
        candidate["paths"]!["/api/v1/items"]!["get"]!["requestBody"]!["content"]!
            ["application/json"]!["schema"]!["properties"]!["note"] =
            new JsonObject { ["type"] = "string" };
        candidate["paths"]!["/api/v1/items"]!["get"]!["parameters"]!.AsArray().Add(
            new JsonObject
            {
                ["name"] = "after",
                ["in"] = "query",
                ["required"] = false,
                ["schema"] = new JsonObject { ["type"] = "string" },
            });
        candidate["paths"]!["/api/v1/new-items"] = new JsonObject
        {
            ["get"] = new JsonObject
            {
                ["responses"] = new JsonObject
                {
                    ["200"] = new JsonObject { ["description"] = "Success" },
                },
            },
        };

        Assert.Empty(OpenApiContractApprover.FindBreakingChanges(baseline, candidate));
    }

    [Fact]
    public void adding_a_required_parameter_to_a_parameterless_operation_is_rejected()
    {
        var baseline = ParseBaseline();
        baseline["paths"]!["/api/v1/items"]!["get"]!.AsObject().Remove("parameters");
        var candidate = ParseBaseline();
        candidate["paths"]!["/api/v1/items"]!["get"]!.AsObject().Remove("parameters");
        candidate["paths"]!["/api/v1/items"]!["get"]!["parameters"] = new JsonArray
        {
            new JsonObject
            {
                ["name"] = "tenant",
                ["in"] = "header",
                ["required"] = true,
                ["schema"] = new JsonObject { ["type"] = "string" },
            },
        };

        Assert.NotEmpty(OpenApiContractApprover.FindBreakingChanges(baseline, candidate));
    }

    [Fact]
    public void adding_a_required_referenced_parameter_to_a_parameterless_operation_is_rejected()
    {
        var baseline = ParseBaseline();
        baseline["paths"]!["/api/v1/items"]!["get"]!.AsObject().Remove("parameters");
        var candidate = ParseBaseline();
        candidate["paths"]!["/api/v1/items"]!["get"]!.AsObject().Remove("parameters");
        candidate["components"] = new JsonObject
        {
            ["parameters"] = new JsonObject
            {
                ["Tenant"] = new JsonObject
                {
                    ["name"] = "tenant",
                    ["in"] = "header",
                    ["required"] = true,
                    ["schema"] = new JsonObject { ["type"] = "string" },
                },
            },
        };
        candidate["paths"]!["/api/v1/items"]!["get"]!["parameters"] = new JsonArray
        {
            new JsonObject { ["$ref"] = "#/components/parameters/Tenant" },
        };

        Assert.NotEmpty(OpenApiContractApprover.FindBreakingChanges(baseline, candidate));
    }

    [Theory]
    [InlineData("remove-operation")]
    [InlineData("rename-operation")]
    [InlineData("rename-route")]
    [InlineData("remove-parameter")]
    [InlineData("rename-parameter")]
    [InlineData("require-optional-parameter")]
    [InlineData("restrict-parameter-type")]
    [InlineData("remove-property")]
    [InlineData("rename-property")]
    [InlineData("restrict-property-type")]
    [InlineData("require-optional-request-property")]
    public void breaking_contract_changes_are_rejected(string change)
    {
        var baseline = ParseBaseline();
        var candidate = ParseBaseline();

        ApplyBreakingChange(candidate, change);

        Assert.NotEmpty(OpenApiContractApprover.FindBreakingChanges(baseline, candidate));
    }

    private static JsonObject ParseBaseline() =>
        JsonNode.Parse(BaselineContract)!.AsObject();

    private static void AssertTripCollectionContract(JsonNode document)
    {
        var operation = document["paths"]?["/api/v1/trips"]?["get"];
        Assert.NotNull(operation);
        Assert.Collection(
            operation!["parameters"]!.AsArray(),
            parameter => Assert.Equal("limit", parameter?["name"]?.GetValue<string>()),
            parameter => Assert.Equal("after", parameter?["name"]?.GetValue<string>()));
        AssertBadRequestSchemaIncludesBothProblemShapes(document, "/api/v1/trips");
    }

    private static void AssertExpenseContracts(JsonNode document)
    {
        const string collectionPath = "/api/v1/campers/{camperId}/expenses";
        const string itemPath = "/api/v1/expenses/{expenseId}";
        var collection = document["paths"]?[collectionPath];
        Assert.NotNull(collection?["get"]);
        Assert.NotNull(collection?["post"]);
        var collectionParameters = collection!["get"]!["parameters"]!.AsArray();
        Assert.Contains(collectionParameters, parameter => parameter?["name"]?.GetValue<string>() == "tripId"
            && parameter?["in"]?.GetValue<string>() == "query"
            && parameter?["required"]?.GetValue<bool>() != true);
        Assert.NotNull(collection["get"]?["responses"]?["200"]);
        Assert.NotNull(collection["post"]?["responses"]?["201"]);
        Assert.NotNull(collection["post"]?["responses"]?["200"]);
        AssertStrongEtag(collection["post"]?["responses"]?["201"]);
        Assert.NotNull(collection["post"]?["responses"]?["200"]?["headers"]?["ETag"]);
        Assert.NotNull(collection["post"]?["responses"]?["201"]?["headers"]?["Location"]);

        var detail = document["paths"]?[itemPath]?["get"];
        AssertStrongEtag(detail?["responses"]?["200"]);
        var update = document["paths"]?[itemPath]?["put"];
        AssertIfMatchRequired(update);
        AssertStrongEtag(update?["responses"]?["200"]);
        Assert.NotNull(update?["responses"]?["412"]);
        Assert.NotNull(update?["responses"]?["428"]);

        var delete = document["paths"]?[itemPath]?["delete"];
        AssertIfMatchRequired(delete);
        Assert.NotNull(delete?["responses"]?["204"]);
        Assert.NotNull(delete?["responses"]?["412"]);
        Assert.NotNull(delete?["responses"]?["428"]);

        var schemas = document["components"]?["schemas"];
        var money = schemas?["ExpenseMoneyRequest"];
        AssertRequestSchema(
            schemas?["CreateExpenseRequest"],
            ["id", "amount", "category", "incurredOnUtc"],
            ["description", "odometerKm", "tripId"]);
        AssertRequestSchema(
            schemas?["UpdateExpenseRequest"],
            ["amount", "category", "incurredOnUtc"],
            ["description", "odometerKm", "tripId"]);
        AssertRequestSchema(money, ["amount", "currency"], []);
        AssertMoneyAmountSchema(money?["properties"]?["amount"]);
        AssertMoneyCurrencySchema(money?["properties"]?["currency"]);
        Assert.Contains("diverso da zero", money?["properties"]?["amount"]?["description"]?.GetValue<string>(),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("4 decimali", money?["properties"]?["amount"]?["description"]?.GetValue<string>(),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("3 lettere ASCII maiuscole",
            money?["properties"]?["currency"]?["description"]?.GetValue<string>(),
            StringComparison.OrdinalIgnoreCase);

        var responseMoney = schemas?["Money"];
        Assert.NotNull(responseMoney);
        Assert.Equal(
            ["amount", "currency"],
            responseMoney!["required"]!.AsArray()
                .Select(property => property!.GetValue<string>())
                .Order()
                .ToArray());
        AssertNonNullableSchema(responseMoney["properties"]?["amount"]);
        AssertNonNullableSchema(responseMoney["properties"]?["currency"]);
        AssertMoneyAmountSchema(responseMoney["properties"]?["amount"]);
        AssertMoneyCurrencySchema(responseMoney["properties"]?["currency"]);

        Assert.Equal(
            [0, 1, 2, 3, 4, 5, 6],
            schemas?["ExpenseCategory"]?["enum"]?.AsArray()
                .Select(value => value?.GetValue<int>())
                .ToArray());
        Assert.Contains("0 Fuel", schemas?["ExpenseCategory"]?["description"]?.GetValue<string>());
        Assert.Contains("6 Other", schemas?["ExpenseCategory"]?["description"]?.GetValue<string>());
    }

    private static void AssertChecklistContracts(JsonNode document)
    {
        const string collection = "/api/v1/trips/{tripId}/checklists";
        const string detail = "/api/v1/trips/{tripId}/checklists/{checklistId}";
        const string items = "/api/v1/trips/{tripId}/checklists/{checklistId}/items";
        const string order = "/api/v1/trips/{tripId}/checklists/{checklistId}/items/order";
        const string item = "/api/v1/trips/{tripId}/checklists/{checklistId}/items/{itemId}";

        var create = document["paths"]?[collection]?["post"];
        AssertIfMatchRequired(create);
        Assert.NotNull(create?["responses"]?["201"]?["headers"]?["ETag"]);
        Assert.NotNull(create?["responses"]?["200"]?["headers"]?["ETag"]);
        Assert.NotNull(create?["responses"]?["201"]?["headers"]?["Location"]);
        Assert.NotNull(document["paths"]?[detail]?["get"]?["responses"]?["200"]?["headers"]?["ETag"]);

        var update = document["paths"]?[detail]?["put"];
        AssertIfMatchRequired(update);
        Assert.NotNull(update?["responses"]?["200"]?["headers"]?["ETag"]);
        var delete = document["paths"]?[detail]?["delete"];
        AssertIfMatchRequired(delete);
        Assert.NotNull(delete?["responses"]?["204"]?["headers"]?["ETag"]);

        var createItem = document["paths"]?[items]?["post"];
        AssertIfMatchRequired(createItem);
        AssertStrongEtag(createItem?["responses"]?["201"]);
        AssertStrongEtag(createItem?["responses"]?["200"]);
        Assert.NotNull(createItem?["responses"]?["201"]?["headers"]?["Location"]);
        AssertStrongEtag(document["paths"]?[item]?["get"]?["responses"]?["200"]);
        AssertIfMatchRequired(document["paths"]?[order]?["put"]);
        AssertIfMatchRequired(document["paths"]?[item]?["put"]);
        AssertIfMatchRequired(document["paths"]?[item]?["delete"]);

        var schemas = document["components"]?["schemas"];
        AssertRequestSchema(schemas?["CreateChecklistRequest"], ["id", "name", "kind"], []);
        AssertRequestSchema(schemas?["UpdateChecklistRequest"], ["name", "kind"], []);
        AssertRequestSchema(schemas?["CreateChecklistItemRequest"], ["id", "text"], []);
        AssertRequestSchema(schemas?["UpdateChecklistItemRequest"], ["text", "isDone"], []);
        AssertRequestSchema(schemas?["ReorderChecklistItemsRequest"], ["itemIds"], []);
        Assert.Equal(
            [0, 1, 2],
            schemas?["ChecklistKind"]?["enum"]?.AsArray()
                .Select(value => value?.GetValue<int>())
                .ToArray());
    }

    private static void AssertMoneyAmountSchema(JsonNode? schema)
    {
        Assert.NotNull(schema);
        Assert.Equal("number", schema!["type"]?.GetValue<string>());
        Assert.Equal(-999999999999999.9999m, schema!["minimum"]!.GetValue<decimal>());
        Assert.Equal(999999999999999.9999m, schema["maximum"]!.GetValue<decimal>());
        Assert.Equal(0.0001m, schema["multipleOf"]!.GetValue<decimal>());
        Assert.Equal(0, schema["not"]?["enum"]?[0]?.GetValue<int>());
    }

    private static void AssertMoneyCurrencySchema(JsonNode? schema)
    {
        Assert.NotNull(schema);
        Assert.Equal("^[A-Z]{3}$", schema!["pattern"]?.GetValue<string>());
        AssertNonNullableSchema(schema);
    }

    private static void AssertRequestSchema(
        JsonNode? schema,
        string[] requiredProperties,
        string[] optionalProperties)
    {
        Assert.NotNull(schema);
        var required = schema!["required"]!.AsArray()
            .Select(property => property!.GetValue<string>())
            .ToArray();
        Assert.Equal(requiredProperties.Order().ToArray(), required.Order().ToArray());

        foreach (var propertyName in requiredProperties)
        {
            AssertNonNullableSchema(schema["properties"]?[propertyName]);
        }

        foreach (var propertyName in optionalProperties)
        {
            Assert.DoesNotContain(propertyName, required);
        }
    }

    private static void AssertNonNullableSchema(JsonNode? schema)
    {
        Assert.NotNull(schema);
        if (schema!["type"] is JsonArray types)
        {
            Assert.DoesNotContain(types, type => type?.GetValue<string>() == "null");
        }
        else
        {
            Assert.NotEqual("null", schema["type"]?.GetValue<string>());
        }

        if (schema["oneOf"] is JsonArray alternatives)
        {
            Assert.DoesNotContain(
                alternatives,
                alternative => alternative?["type"]?.GetValue<string>() == "null");
        }
    }

    private static void AssertIfMatchRequired(JsonNode? operation)
    {
        Assert.NotNull(operation);
        Assert.Contains(
            operation!["parameters"]!.AsArray(),
            parameter => parameter?["name"]?.GetValue<string>() == "If-Match"
                && parameter?["in"]?.GetValue<string>() == "header"
                && parameter?["required"]?.GetValue<bool>() == true);
        var ifMatch = operation["parameters"]!.AsArray().Single(
            parameter => parameter?["name"]?.GetValue<string>() == "If-Match");
        Assert.Contains("ETag forte", ifMatch?["description"]?.GetValue<string>());
    }

    private static void AssertStrongEtag(JsonNode? response)
    {
        var etag = response?["headers"]?["ETag"];
        Assert.NotNull(etag);
        var description = etag!["description"]?.GetValue<string>();
        Assert.Contains("ETag forte", description);
        Assert.Contains("\"<base64url RowVersion>\"", description);
    }

    private static void AssertTripUpdateConcurrencyContract(JsonNode document)
    {
        var operation = document["paths"]?["/api/v1/trips/{tripId}"]?["put"];
        Assert.NotNull(operation);
        Assert.Contains(
            operation!["parameters"]!.AsArray(),
            parameter => parameter?["name"]?.GetValue<string>() == "If-Match"
                && parameter?["in"]?.GetValue<string>() == "header"
                && parameter?["required"]?.GetValue<bool>() == true);
        Assert.NotNull(operation["responses"]?["200"]?["headers"]?["ETag"]);

        var schemas = document["components"]?["schemas"];
        var updateRequest = schemas?["UpdateTripRequest"];
        Assert.DoesNotContain(
            "fuelPricePerLiter",
            updateRequest?["required"]?.AsArray().Select(property => property?.GetValue<string>())
                .OfType<string>() ?? []);
        Assert.Contains(
            "#/components/schemas/UpdateTripFuelPriceRequest",
            updateRequest?["properties"]?["fuelPricePerLiter"]?["oneOf"]?.AsArray()
                .Select(item => item?["$ref"]?.GetValue<string>()).OfType<string>() ?? []);

        var fuelPriceRequest = schemas?["UpdateTripFuelPriceRequest"];
        AssertRequestSchema(fuelPriceRequest, ["amount", "currency"], []);
        Assert.Equal(0.0001m, fuelPriceRequest?["properties"]?["amount"]?["minimum"]?.GetValue<decimal>());
        Assert.Equal(
            999999999999999.9999m,
            fuelPriceRequest?["properties"]?["amount"]?["maximum"]?.GetValue<decimal>());
        Assert.Equal(
            "^[A-Z]{3}$",
            fuelPriceRequest?["properties"]?["currency"]?["pattern"]?.GetValue<string>());

        var responsePrice = schemas?["TripDetailsResponse"]?["properties"]?["fuelPricePerLiter"];
        Assert.Contains(
            "#/components/schemas/Money",
            responsePrice?["oneOf"]?.AsArray()
                .Select(item => item?["$ref"]?.GetValue<string>()).OfType<string>() ?? []);
    }

    private static void AssertTripTransitionContract(JsonNode document)
    {
        const string path = "/api/v1/trips/{tripId}/transitions";
        var operation = document["paths"]?[path]?["post"];
        Assert.NotNull(operation);
        Assert.Contains(
            operation!["parameters"]!.AsArray(),
            parameter => parameter?["name"]?.GetValue<string>() == "If-Match"
                && parameter?["in"]?.GetValue<string>() == "header"
                && parameter?["required"]?.GetValue<bool>() == true);
        Assert.Equal(
            "#/components/schemas/TransitionTripRequest",
            operation["requestBody"]?["content"]?["application/json"]?["schema"]?["$ref"]?.GetValue<string>());
        var requestSchema = document["components"]?["schemas"]?["TransitionTripRequest"];
        var requiredProperties = requestSchema?["required"]?.AsArray();
        Assert.NotNull(requiredProperties);
        Assert.Contains("status", requiredProperties!.Select(item => item?.GetValue<string>()).OfType<string>());
        Assert.Null(requestSchema?["properties"]?["status"]?["oneOf"]);
        var allowedStatuses = document["components"]?["schemas"]?["TripStatus"]?["enum"]?.AsArray()
            .Select(item => item?.GetValue<int>())
            .ToArray();
        Assert.Equal([0, 1, 2, 3], allowedStatuses);
        Assert.NotNull(operation["responses"]?["200"]?["headers"]?["ETag"]);
        foreach (var status in new[] { "400", "401", "404", "409", "412", "428" })
        {
            Assert.NotNull(operation["responses"]?[status]);
        }

        var response = document["components"]?["schemas"]?["TripDetailsResponse"]?["properties"];
        Assert.NotNull(response?["actualStartOnUtc"]);
        Assert.NotNull(response?["actualEndOnUtc"]);
    }

    private static void AssertBadRequestSchemaIncludesBothProblemShapes(JsonNode document, string route)
    {
        var schema = document["paths"]?[route]?["get"]?["responses"]?["400"]?
            ["content"]?["application/problem+json"]?["schema"];
        Assert.NotNull(schema);
        var references = schema!["anyOf"]!.AsArray()
            .Select(item => item?["$ref"]?.GetValue<string>())
            .ToArray();
        Assert.Contains("#/components/schemas/ProblemDetails", references);
        Assert.Contains("#/components/schemas/HttpValidationProblemDetails", references);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Roamly.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Radice repository non trovata.");
    }

    private static void ApplyBreakingChange(JsonObject candidate, string change)
    {
        var paths = candidate["paths"]!.AsObject();
        var operation = paths["/api/v1/items"]!["get"]!.AsObject();

        switch (change)
        {
            case "remove-operation":
                paths["/api/v1/items"]!["get"] = null;
                break;
            case "rename-operation":
                operation["operationId"] = "GetItems";
                break;
            case "rename-route":
                paths["/api/v1/items"]!["get"] = null;
                paths["/api/v1/renamed-items"] = new JsonObject
                {
                    ["get"] = operation.DeepClone(),
                };
                break;
            case "remove-parameter":
            case "rename-parameter":
            case "require-optional-parameter":
            case "restrict-parameter-type":
                var parameter = operation["parameters"]![0]!.AsObject();
                if (change == "remove-parameter")
                {
                    operation["parameters"]!.AsArray().Clear();
                }
                else if (change == "rename-parameter")
                {
                    parameter["name"] = "pageSize";
                }
                else if (change == "restrict-parameter-type")
                {
                    parameter["schema"]!["type"] = "string";
                }
                else
                {
                    parameter["required"] = true;
                }

                break;
            case "remove-property":
            case "rename-property":
            case "restrict-property-type":
                var properties = operation["responses"]!["200"]!["content"]!
                    ["application/json"]!["schema"]!["properties"]!.AsObject();
                if (change == "remove-property")
                {
                    properties.Remove("name");
                }
                else if (change == "rename-property")
                {
                    var nameSchema = properties["name"]!.DeepClone();
                    properties.Remove("name");
                    properties["displayName"] = nameSchema;
                }
                else
                {
                    properties["name"]!["type"] = "integer";
                }

                break;
            case "require-optional-request-property":
                operation["requestBody"]!["content"]!["application/json"]!["schema"]!["required"]!
                    .AsArray().Add("filter");
                break;
        }
    }
}
