using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FinancialMonitor.Api.Models;

namespace FinancialMonitor.Api.Contracts;

/// <summary>
/// Body of POST /api/transactions, as sent by the external system.
/// Every field is nullable so a missing field can be told apart from a default value
/// (otherwise a missing amount would silently become 0 and a missing id Guid.Empty).
/// [ApiController] runs this validation before the action and returns 400 with an error per field.
/// </summary>
public class TransactionRequest : IValidatableObject
{
    [Required]
    public Guid? TransactionId { get; init; }

    [Required]
    public decimal? Amount { get; init; }

    [Required]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Currency must be a 3-letter uppercase ISO code, e.g. USD.")]
    public string? Currency { get; init; }

    // An enum, not a string: an unknown name fails deserialization (see ApiJson).
    [Required]
    public TransactionStatus? Status { get; init; }

    [Required]
    public DateTimeOffset? Timestamp { get; init; }

    // Rules with no simple built-in attribute.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TransactionId == Guid.Empty)
            yield return new ValidationResult("TransactionId must not be empty.", [JsonName(nameof(TransactionId))]);

        if (Amount <= 0)
            yield return new ValidationResult("Amount must be greater than 0.", [JsonName(nameof(Amount))]);
    }

    // Attribute errors are already keyed by JSON name (see Program.cs); errors returned from here are not,
    // so the name is converted with the same camelCase policy the JSON uses.
    private static string JsonName(string propertyName) => JsonNamingPolicy.CamelCase.ConvertName(propertyName);
}
