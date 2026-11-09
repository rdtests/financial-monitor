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

    [Theory]
    [InlineData("1500.50")]
    [InlineData("0.30")]
    [InlineData("12345678901234567.89")]
    public void ToDomain_keeps_the_exact_decimal_amount(string amount)
    {
        var expected = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);
        var request = new TransactionRequest
        {
            TransactionId = Id,
            Amount = expected,
            Currency = "USD",
            Status = TransactionStatus.Pending,
            Timestamp = Timestamp
        };

        request.ToDomain().Amount.ShouldBe(expected);
    }

    [Fact]
    public void Amount_from_json_is_not_subject_to_floating_point_rounding()
    {
        // 0.1 + 0.2 is 0.30000000000000004 as a double; as a decimal it must stay exact.
        var request = JsonSerializer.Deserialize<TransactionRequest>(
            """{"amount": 0.1}""", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        (request.Amount + 0.2m).ShouldBe(0.3m);
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
