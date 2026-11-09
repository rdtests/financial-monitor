using System.Text.Json;
using FinancialMonitor.Api.Contracts;
using FinancialMonitor.Api.Models;

namespace FinancialMonitor.Tests.Contracts;

public class TransactionMappingsTests
{
    private static readonly Guid Id = Guid.Parse("3f2b8c1e-4d5a-4b6c-8e9f-0a1b2c3d4e5f");
    private static readonly DateTimeOffset Timestamp = new(2024, 1, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToDomain_maps_all_fields()
    {
        var request = new TransactionRequest
        {
            TransactionId = Id,
            Amount = 1500.50m,
            Currency = "USD",
            Status = TransactionStatus.Completed,
            Timestamp = Timestamp
        };

        var transaction = request.ToDomain();

        transaction.ShouldBe(new Transaction(Id, 1500.50m, "USD", TransactionStatus.Completed, Timestamp));
    }

    [Fact]
    public void ToResponse_maps_all_fields()
    {
        var transaction = new Transaction(Id, 1500.50m, "USD", TransactionStatus.Failed, Timestamp);

        var response = transaction.ToResponse();

        response.ShouldBe(new TransactionResponse(Id, 1500.50m, "USD", TransactionStatus.Failed, Timestamp));
    }

    [Fact]
    public void TransactionResponse_serializes_to_the_assignment_json_shape()
    {
        var response = new TransactionResponse(Id, 1500.50m, "USD", TransactionStatus.Completed, Timestamp);

        // Web defaults (camelCase) + ApiJson: exactly what the controllers and SignalR are configured with.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJson.Configure(options);

        var json = JsonSerializer.Serialize(response, options);

        json.ShouldBe(
            """{"transactionId":"3f2b8c1e-4d5a-4b6c-8e9f-0a1b2c3d4e5f","amount":1500.50,"currency":"USD","status":"Completed","timestamp":"2024-01-15T10:00:00+00:00"}""");
    }
}
