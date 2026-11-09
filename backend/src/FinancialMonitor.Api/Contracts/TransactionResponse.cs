using FinancialMonitor.Api.Models;

namespace FinancialMonitor.Api.Contracts;

/// <summary>
/// A transaction as the API exposes it: returned by GET /api/transactions and pushed by the
/// SignalR "TransactionUpdated" event, so the frontend handles a single shape.
/// </summary>
public record TransactionResponse(
    Guid TransactionId,
    decimal Amount,
    string Currency,
    TransactionStatus Status,
    DateTimeOffset Timestamp);
