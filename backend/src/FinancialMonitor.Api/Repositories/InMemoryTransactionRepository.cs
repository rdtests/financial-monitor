using FinancialMonitor.Api.Models;

namespace FinancialMonitor.Api.Repositories;

/// <summary>
/// Keeps transactions in memory, keyed by TransactionId.
/// Not thread-safe yet: the lock is added in a later step, only after concurrency tests fail without it.
/// </summary>
public class InMemoryTransactionRepository : ITransactionRepository
{
    private readonly Dictionary<Guid, Transaction> _transactions = new();

    public UpsertResult Upsert(Transaction transaction)
    {
        var exists = _transactions.ContainsKey(transaction.TransactionId);
        _transactions[transaction.TransactionId] = transaction;
        return exists ? UpsertResult.Updated : UpsertResult.Created;
    }

    public IReadOnlyList<Transaction> GetLatest(int count) =>
        _transactions.Values.Take(count).ToList();
}
