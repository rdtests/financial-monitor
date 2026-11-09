using FinancialMonitor.Api.Models;

namespace FinancialMonitor.Api.Repositories;

/// <summary>
/// Keeps the latest transactions in memory, keyed by TransactionId, up to a fixed capacity.
/// Not thread-safe yet: the lock is added in a later step, only after concurrency tests fail without it.
/// </summary>
public class InMemoryTransactionRepository(int capacity = InMemoryTransactionRepository.DefaultCapacity)
    : ITransactionRepository
{
    public const int DefaultCapacity = 1000;

    private readonly Dictionary<Guid, Transaction> _transactions = new();

    public UpsertResult Upsert(Transaction transaction)
    {
        if (_transactions.TryGetValue(transaction.TransactionId, out var stored))
        {
            // Updates can arrive out of order; an older one must not overwrite newer data.
            // An equal timestamp is accepted (e.g. Pending -> Completed within the same second, or a retry).
            if (transaction.Timestamp < stored.Timestamp)
                return UpsertResult.IgnoredStale;

            _transactions[transaction.TransactionId] = transaction;
            return UpsertResult.Updated;
        }

        // Make room before inserting, so a transaction reported as Created is always in the store.
        if (_transactions.Count >= capacity)
            RemoveOldest();

        _transactions[transaction.TransactionId] = transaction;
        return UpsertResult.Created;
    }

    public IReadOnlyList<Transaction> GetLatest(int count) =>
        // ToList() copies, so the caller gets a snapshot that later writes cannot change.
        _transactions.Values
            .OrderByDescending(t => t.Timestamp)
            .Take(count)
            .ToList();

    // O(n) scan: with ~1000 items this is negligible, and simpler than maintaining a second sorted index.
    private void RemoveOldest()
    {
        var oldest = _transactions.Values.MinBy(t => t.Timestamp)!;
        _transactions.Remove(oldest.TransactionId);
    }
}
