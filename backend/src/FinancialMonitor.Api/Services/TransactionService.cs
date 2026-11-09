using FinancialMonitor.Api.Models;
using FinancialMonitor.Api.Notifiers;
using FinancialMonitor.Api.Repositories;

namespace FinancialMonitor.Api.Services;

public class TransactionService(ITransactionRepository repository, ITransactionNotifier notifier)
    : ITransactionService
{
    public async Task<UpsertResult> ProcessAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        // Store first, then broadcast: clients are only told about data that is already persisted.
        var result = repository.Upsert(transaction);
        await notifier.NotifyAsync(transaction, cancellationToken);
        return result;
    }
}
