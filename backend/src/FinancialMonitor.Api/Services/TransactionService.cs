using FinancialMonitor.Api.Models;
using FinancialMonitor.Api.Notifiers;
using FinancialMonitor.Api.Repositories;

namespace FinancialMonitor.Api.Services;

public class TransactionService(
    ITransactionRepository repository,
    ITransactionNotifier notifier,
    ILogger<TransactionService> logger) : ITransactionService
{
    // Matches the number of rows the dashboard displays.
    private const int SnapshotSize = 500;

    public async Task<UpsertResult> ProcessAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        // Store first, then broadcast: clients are only told about data that is already persisted.
        var result = repository.Upsert(transaction);

        // A stale update changed nothing, so there is nothing to tell the clients.
        if (result != UpsertResult.IgnoredStale)
            await NotifySafelyAsync(transaction, cancellationToken);

        return result;
    }

    // The transaction is already stored, so a failed broadcast must not fail the request.
    // Clients catch up when they reload the snapshot (on reconnect).
    private async Task NotifySafelyAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await notifier.NotifyAsync(transaction, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Broadcasting transaction {TransactionId} failed", transaction.TransactionId);
        }
    }

    public IReadOnlyList<Transaction> GetLatest() => repository.GetLatest(SnapshotSize);
}
