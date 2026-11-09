namespace FinancialMonitor.Api.Models;

/// <summary>
/// Domain model of a transaction. Immutable: an update is a new instance with the same TransactionId.
/// Amount is decimal (never double) to avoid binary floating-point rounding on money.
/// </summary>
public record Transaction(
    Guid TransactionId,
    decimal Amount,
    string Currency,
    TransactionStatus Status,
    DateTimeOffset Timestamp);
