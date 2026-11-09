using FinancialMonitor.Api.Models;

namespace FinancialMonitor.Api.Contracts;

/// <summary>Converts between the API contract and the domain model, at the API boundary only.</summary>
public static class TransactionMappings
{
    // Called only after validation succeeded, so every field is known to be present.
    public static Transaction ToDomain(this TransactionRequest request) => new(
        request.TransactionId!.Value,
        request.Amount!.Value,
        request.Currency!,
        request.Status!.Value,
        request.Timestamp!.Value);

    public static TransactionResponse ToResponse(this Transaction transaction) => new(
        transaction.TransactionId,
        transaction.Amount,
        transaction.Currency,
        transaction.Status,
        transaction.Timestamp);
}
