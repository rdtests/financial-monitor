using System.Net.Http.Json;
using System.Text.Json;
using FinancialMonitor.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinancialMonitor.Tests.Integration;

/// <summary>
/// Concurrency end to end: parallel HTTP requests go through the whole chain
/// (Controller → Service → Repository), not just the repository in isolation.
/// Each test uses its own application instance so data from other tests cannot interfere.
/// </summary>
public class TransactionsConcurrencyApiTests
{
    private static readonly DateTimeOffset BaseTime = new(2024, 1, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Parallel_posts_of_different_ids_are_all_stored()
    {
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();
        var ids = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid()).ToList();

        var responses = await Task.WhenAll(ids.Select((id, i) => PostAsync(client, id, BaseTime.AddSeconds(i))));

        responses.ShouldAllBe(r => r.IsSuccessStatusCode);
        var stored = await GetAllAsync(client);
        stored.Select(t => t.TransactionId).ShouldBe(ids, ignoreOrder: true);
    }

    [Fact]
    public async Task Parallel_posts_for_the_same_id_keep_the_newest_timestamp()
    {
        await using var app = new WebApplicationFactory<Program>();
        var client = app.CreateClient();
        var id = Guid.NewGuid();
        var timestamps = Enumerable.Range(0, 100)
            .Select(i => BaseTime.AddSeconds(i))
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        var responses = await Task.WhenAll(timestamps.Select(ts => PostAsync(client, id, ts)));

        responses.ShouldAllBe(r => r.IsSuccessStatusCode);
        var stored = await GetAllAsync(client);
        stored.Single().Timestamp.ShouldBe(timestamps.Max());
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Guid id, DateTimeOffset timestamp) =>
        client.PostAsJsonAsync("/api/transactions", new
        {
            transactionId = id,
            amount = 10m,
            currency = "USD",
            status = "Pending",
            timestamp
        });

    private static async Task<List<TransactionResponse>> GetAllAsync(HttpClient client)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJson.Configure(options);
        return (await client.GetFromJsonAsync<List<TransactionResponse>>("/api/transactions", options))!;
    }
}
