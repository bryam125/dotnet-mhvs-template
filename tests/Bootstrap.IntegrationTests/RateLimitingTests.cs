using System.Net;
using MyApp.Tests.Bootstrap.Support;
using Xunit;

namespace MyApp.Tests.Bootstrap;

/// <summary>
/// El limitador global (ver ARCHITECTURE.md → "Rate limiting") responde 429 al superar el límite
/// configurado. Usa <see cref="RateLimitedBootstrapFactory"/>, con el límite bajado a
/// <see cref="RateLimitedBootstrapFactory.PermitLimit"/> requests/minuto solo para este test — la app
/// real usa el valor de <c>appsettings.json</c> (100/minuto).
/// </summary>
public class RateLimitingTests(RateLimitedBootstrapFactory factory)
    : IClassFixture<RateLimitedBootstrapFactory>
{
    [Fact]
    public async Task RequestsWithinTheLimit_Succeed_AndTheNextOneIsRejected()
    {
        var client = factory.CreateClient();

        for (var i = 0; i < RateLimitedBootstrapFactory.PermitLimit; i++)
        {
            var response = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var rejected = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }
}
