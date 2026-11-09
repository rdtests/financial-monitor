using FinancialMonitor.Api.Models;

namespace FinancialMonitor.Api.Contracts;

/// <summary>
/// Body of POST /api/transactions, as sent by the external system.
/// Every field is nullable so a missing field can be told apart from a default value
/// (otherwise a missing amount would silently become 0 and a missing id Guid.Empty).
/// </summary>
public class TransactionRequest
{
    public Guid? TransactionId { get; init; }

    public decimal? Amount { get; init; }

    public string? Currency { get; init; }

    // An enum, not a string: an unknown name fails deserialization (see ApiJson).
    public TransactionStatus? Status { get; init; }

    public DateTimeOffset? Timestamp { get; init; }
}
