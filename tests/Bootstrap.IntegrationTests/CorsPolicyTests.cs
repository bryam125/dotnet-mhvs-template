using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyApp.Tests.Bootstrap.Support;
using Xunit;

namespace MyApp.Tests.Bootstrap;

/// <summary>
/// La política "Default" existe y, por defecto (sin <c>Cors:AllowedOrigins</c> configurado), no
/// permite ningún origen — ver ARCHITECTURE.md → "CORS": es la postura intencional del template, no
/// un olvido. Se verifica inspeccionando el servicio registrado, sin simular un request cross-origin
/// real (un preflight sin la política esperada respondería igual, sin distinguir "no hay política" de
/// "hay política, pero no permite este origen").
/// </summary>
[Collection(BootstrapCollection.Name)]
public class CorsPolicyTests(BootstrapFactory factory)
{
    [Fact]
    public void DefaultPolicy_IsRegistered_WithNoOriginsAllowedByDefault()
    {
        var corsOptions = factory.Services.GetRequiredService<IOptions<CorsOptions>>().Value;

        var policy = corsOptions.GetPolicy("Default");

        Assert.NotNull(policy);
        Assert.Empty(policy.Origins);
    }
}
