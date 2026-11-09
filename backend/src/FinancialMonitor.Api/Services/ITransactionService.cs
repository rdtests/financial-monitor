using FinancialMonitor.Api.Models;

namespace FinancialMonitor.Api.Services;

public interface ITransactionService
{
    /// <summary>Stores the transaction and broadcasts it to connected clients.</summary>
    Task<UpsertResult> ProcessAsync(Transaction transaction, CancellationToken cancellationToken);
}
