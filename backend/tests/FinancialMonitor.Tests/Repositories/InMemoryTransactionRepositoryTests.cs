using FinancialMonitor.Api.Models;
using FinancialMonitor.Api.Repositories;

namespace FinancialMonitor.Tests.Repositories;

public class InMemoryTransactionRepositoryTests
{
    private static readonly DateTimeOffset BaseTime = new(2024, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly InMemoryTransactionRepository _repository = new();

    [Fact]
    public void Upsert_new_id_returns_Created_and_is_returned_by_GetLatest()
    {
        var transaction = CreateTransaction(Guid.NewGuid(), TransactionStatus.Pending, BaseTime);

        var result = _repository.Upsert(transaction);

        result.ShouldBe(UpsertResult.Created);
        _repository.GetLatest(10).ShouldBe([transaction]);
    }

    [Fact]
    public void Upsert_existing_id_with_newer_timestamp_returns_Updated_and_replaces_value()
    {
        var id = Guid.NewGuid();
        var original = CreateTransaction(id, TransactionStatus.Pending, BaseTime);
        var update = CreateTransaction(id, TransactionStatus.Completed, BaseTime.AddSeconds(5));
        _repository.Upsert(original);

        var result = _repository.Upsert(update);

        result.ShouldBe(UpsertResult.Updated);
        _repository.GetLatest(10).ShouldBe([update]);
    }

    private static Transaction CreateTransaction(Guid id, TransactionStatus status, DateTimeOffset timestamp) =>
        new(id, 1500.50m, "USD", status, timestamp);
}
