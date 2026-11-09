using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinancialMonitor.Api.Contracts;
using FinancialMonitor.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinancialMonitor.Tests.Integration;

public class TransactionsApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateTimeOffset BaseTime = new(2024, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly HttpClient _client = factory.CreateClient();

    // Reads responses with the same settings the API writes them with (status as a string).
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJson.Configure(options);
        return options;
    }

    [Fact]
    public async Task Post_new_transaction_returns_201_Created_and_Get_returns_it()
    {
        var id = Guid.NewGuid();

        var response = await PostAsync(id, "Pending", BaseTime);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<IngestResponse>();
        body.ShouldBe(new IngestResponse(id, "Created"));

        var all = await _client.GetFromJsonAsync<List<TransactionResponse>>("/api/transactions", JsonOptions);
        all.ShouldNotBeNull();
        all.ShouldContain(new TransactionResponse(id, 1500.50m, "USD", TransactionStatus.Pending, BaseTime));
    }

    [Fact]
    public async Task Post_existing_id_with_newer_timestamp_returns_200_Updated()
    {
        var id = Guid.NewGuid();
        await PostAsync(id, "Pending", BaseTime);

        var response = await PostAsync(id, "Completed", BaseTime.AddSeconds(5));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IngestResponse>();
        body.ShouldBe(new IngestResponse(id, "Updated"));
    }

    // Sent as plain JSON in the assignment format, exactly as an external system would.
    private Task<HttpResponseMessage> PostAsync(Guid id, string status, DateTimeOffset timestamp) =>
        _client.PostAsJsonAsync("/api/transactions", new
        {
            transactionId = id,
            amount = 1500.50m,
            currency = "USD",
            status,
            timestamp
        });
}
