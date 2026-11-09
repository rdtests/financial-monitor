using FinancialMonitor.Api.Models;

namespace FinancialMonitor.Api.Repositories;

public interface ITransactionRepository
{
    /// <summary>Inserts a new transaction or updates the existing one with the same TransactionId.</summary>
    UpsertResult Upsert(Transaction transaction);

    IReadOnlyList<Transaction> GetLatest(int count);
}
