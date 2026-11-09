using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace FinancialMonitor.Tests.Integration;

public class TransactionsHubTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Connected_client_receives_TransactionUpdated_in_the_assignment_json_shape()
    {
        var id = Guid.NewGuid();
        await using var connection = await ConnectAsync();
        var received = ListenFor(connection, id);

        await PostAsync(id);

        var json = await received.WaitAsync(ReceiveTimeout);
        json.GetProperty("transactionId").GetGuid().ShouldBe(id);
        json.GetProperty("amount").GetDecimal().ShouldBe(1500.50m);
        json.GetProperty("currency").GetString().ShouldBe("USD");
        json.GetProperty("status").GetString().ShouldBe("Completed");
        json.GetProperty("timestamp").GetDateTimeOffset()
            .ShouldBe(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Every_connected_client_receives_the_update()
    {
        var id = Guid.NewGuid();
        await using var first = await ConnectAsync();
        await using var second = await ConnectAsync();
        var receivedByFirst = ListenFor(first, id);
        var receivedBySecond = ListenFor(second, id);

        await PostAsync(id);

        await Task.WhenAll(receivedByFirst, receivedBySecond).WaitAsync(ReceiveTimeout);
    }

    [Fact]
    public async Task Disconnected_client_does_not_affect_the_request_or_the_other_clients()
    {
        var id = Guid.NewGuid();
        var leaving = await ConnectAsync();
        await using var staying = await ConnectAsync();
        var receivedByStaying = ListenFor(staying, id);
        await leaving.DisposeAsync();

        var response = await PostAsync(id);

        response.IsSuccessStatusCode.ShouldBeTrue();
        await receivedByStaying.WaitAsync(ReceiveTimeout);
    }

    private async Task<HubConnection> ConnectAsync()
    {
        // TestServer runs in memory, so the client is routed through its handler (long polling).
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/transactions"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        await connection.StartAsync();
        return connection;
    }

    // Completes with the raw JSON of the first TransactionUpdated event for this id
    // (other tests in the class may post at the same time).
    private static Task<JsonElement> ListenFor(HubConnection connection, Guid id)
    {
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>("TransactionUpdated", json =>
        {
            if (json.GetProperty("transactionId").GetGuid() == id)
                received.TrySetResult(json);
        });
        return received.Task;
    }

    private Task<HttpResponseMessage> PostAsync(Guid id) =>
        factory.CreateClient().PostAsJsonAsync("/api/transactions", new
        {
            transactionId = id,
            amount = 1500.50m,
            currency = "USD",
            status = "Completed",
            timestamp = "2024-01-15T10:00:00Z"
        });
}
