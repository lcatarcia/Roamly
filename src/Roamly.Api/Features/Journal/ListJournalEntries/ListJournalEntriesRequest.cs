using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Validation;

namespace Roamly.Api.Features.Journal.ListJournalEntries;

#pragma warning disable ASP0029
[ValidatableType]
#pragma warning restore ASP0029
public sealed record ListJournalEntriesRequest(
    [property: FromQuery(Name = "limit"), RegularExpression(@"\A(?:[1-9]|[1-9][0-9]|1[0-9]{2}|200)\z")]
    string? Limit,
    [property: FromQuery(Name = "after")] string? After);
