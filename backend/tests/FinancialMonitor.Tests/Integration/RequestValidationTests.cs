using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinancialMonitor.Tests.Integration;

/// <summary>
/// Validation is done by the framework (DataAnnotations + JSON deserialization), so it is tested where it
/// actually runs: a real POST that must return 400 with the error reported on the right field.
/// </summary>
public class RequestValidationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("transactionId")]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("status")]
    [InlineData("timestamp")]
    public async Task Missing_field_returns_400_with_an_error_on_that_field(string field)
    {
        var body = ValidBody();
        body.Remove(field);

        var errors = await PostExpectingValidationErrorsAsync(body);

        errors.ShouldContainKey(field);
    }

    [Fact]
    public async Task Empty_transactionId_returns_400()
    {
        var body = ValidBody();
        body["transactionId"] = Guid.Empty;

        var errors = await PostExpectingValidationErrorsAsync(body);

        errors.ShouldContainKey("transactionId");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10.5)]
    public async Task Zero_or_negative_amount_returns_400(decimal amount)
    {
        var body = ValidBody();
        body["amount"] = amount;

        var errors = await PostExpectingValidationErrorsAsync(body);

        errors.ShouldContainKey("amount");
    }

    [Theory]
    [InlineData("")]
    [InlineData("usd")]
    [InlineData("US")]
    [InlineData("USDT")]
    public async Task Currency_that_is_not_three_uppercase_letters_returns_400(string currency)
    {
        var body = ValidBody();
        body["currency"] = currency;

        var errors = await PostExpectingValidationErrorsAsync(body);

        errors.ShouldContainKey("currency");
    }

    [Theory]
    [InlineData("\"Refunded\"")]
    [InlineData("5")]
    public async Task Unknown_status_returns_400(string statusJson)
    {
        var json = $$"""
            {"transactionId":"{{Guid.NewGuid()}}","amount":10,"currency":"USD","status":{{statusJson}},"timestamp":"2024-01-15T10:00:00Z"}
            """;

        var errors = await PostRawExpectingValidationErrorsAsync(json);

        errors.ShouldContainKey("$.status");
    }

    [Fact]
    public async Task Status_in_a_different_case_is_accepted()
    {
        var body = ValidBody();
        body["status"] = "completed";

        var response = await _client.PostAsJsonAsync("/api/transactions", body);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Wrong_json_type_returns_400_with_ValidationProblemDetails()
    {
        var json = $$"""
            {"transactionId":"{{Guid.NewGuid()}}","amount":"abc","currency":"USD","status":"Pending","timestamp":"2024-01-15T10:00:00Z"}
            """;

        var errors = await PostRawExpectingValidationErrorsAsync(json);

        errors.ShouldContainKey("$.amount");
    }

    [Fact]
    public async Task Malformed_json_returns_400_with_ValidationProblemDetails()
    {
        var errors = await PostRawExpectingValidationErrorsAsync("""{"transactionId": """);

        errors.ShouldNotBeEmpty();
    }

    private static Dictionary<string, object?> ValidBody() => new()
    {
        ["transactionId"] = Guid.NewGuid(),
        ["amount"] = 1500.50m,
        ["currency"] = "USD",
        ["status"] = "Pending",
        ["timestamp"] = "2024-01-15T10:00:00Z"
    };

    private Task<IDictionary<string, string[]>> PostExpectingValidationErrorsAsync(Dictionary<string, object?> body) =>
        PostRawExpectingValidationErrorsAsync(JsonSerializer.Serialize(body));

    private async Task<IDictionary<string, string[]>> PostRawExpectingValidationErrorsAsync(string json)
    {
        var response = await _client.PostAsync(
            "/api/transactions", new StringContent(json, Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.ShouldNotBeNull();
        return problem.Errors;
    }
}
