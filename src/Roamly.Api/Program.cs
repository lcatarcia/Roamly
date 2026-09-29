using Roamly.Api.Common.Security;
using Roamly.Api.Common.Startup;
using Roamly.Api.Features.Authentication;
using Roamly.Api.Features.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRoamly(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseCors(SecurityRegistration.CorsPolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<CsrfProtectionMiddleware>();

// Health check fuori da /api/v1, anonimi, esclusi da R45 (R62): non e' contratto pubblico.
app.MapHealthChecks("/health/live").AllowAnonymous();
app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains(ObservabilityRegistration.ReadyTag),
}).AllowAnonymous();

app.MapAuthenticationEndpoints();
app.MapIdentityEndpoints();

app.Run();

/// <summary>Punto d'ingresso, reso parziale per essere visibile a <c>WebApplicationFactory</c> (R44).</summary>
public partial class Program;
