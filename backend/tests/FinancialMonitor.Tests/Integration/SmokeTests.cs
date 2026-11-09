using Microsoft.AspNetCore.Mvc.Testing;

namespace FinancialMonitor.Tests.Integration;

// Proves the whole pipeline is wired: the Api host boots in-memory and answers HTTP.
// Later integration tests build on the same WebApplicationFactory<Program> setup.
public class SmokeTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Api_host_starts_and_handles_requests()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/no-such-route");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }
}
