using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinancialMonitor.Tests.Integration;

/// <summary>
/// In the container the API also serves the built frontend from wwwroot, so the browser uses a single origin.
/// The test points the web root at a temp folder with a stand-in index.html.
/// </summary>
public class FrontendHostingTests : IDisposable
{
    private const string IndexMarker = "<!-- financial-monitor index -->";

    private readonly string _webRoot = Directory.CreateTempSubdirectory("fm-wwwroot-").FullName;
    private readonly WebApplicationFactory<Program> _factory;

    public FrontendHostingTests()
    {
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), $"<html>{IndexMarker}</html>");
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseWebRoot(_webRoot));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/monitor")]
    [InlineData("/add")]
    public async Task Client_side_routes_return_index_html(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain(IndexMarker);
    }

    [Fact]
    public async Task Unknown_api_route_returns_404_not_the_html_page()
    {
        var response = await _factory.CreateClient().GetAsync("/api/does-not-exist");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Api_routes_still_reach_the_controllers()
    {
        var response = await _factory.CreateClient().GetAsync("/api/transactions");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
    }

    public void Dispose()
    {
        _factory.Dispose();
        Directory.Delete(_webRoot, recursive: true);
    }
}
