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

    [Fact]
    public void Upsert_with_older_timestamp_returns_IgnoredStale_and_keeps_stored_value()
    {
        var id = Guid.NewGuid();
        var current = CreateTransaction(id, TransactionStatus.Completed, BaseTime.AddSeconds(5));
        var stale = CreateTransaction(id, TransactionStatus.Pending, BaseTime);
        _repository.Upsert(current);

        var result = _repository.Upsert(stale);

        result.ShouldBe(UpsertResult.IgnoredStale);
        _repository.GetLatest(10).ShouldBe([current]);
    }

    [Fact]
    public void Upsert_with_same_timestamp_returns_Updated_and_replaces_value()
    {
        // E.g. Pending -> Completed within the same second, or a retry of the same request.
        var id = Guid.NewGuid();
        var first = CreateTransaction(id, TransactionStatus.Pending, BaseTime);
        var second = CreateTransaction(id, TransactionStatus.Completed, BaseTime);
        _repository.Upsert(first);

        var result = _repository.Upsert(second);

        result.ShouldBe(UpsertResult.Updated);
        _repository.GetLatest(10).ShouldBe([second]);
    }

    [Theory]
    [InlineData(TransactionStatus.Completed)]
    [InlineData(TransactionStatus.Failed)]
    public void Status_transition_from_Pending_is_stored(TransactionStatus finalStatus)
    {
        var id = Guid.NewGuid();
        _repository.Upsert(CreateTransaction(id, TransactionStatus.Pending, BaseTime));

        _repository.Upsert(CreateTransaction(id, finalStatus, BaseTime.AddSeconds(1)));

        _repository.GetLatest(10).Single().Status.ShouldBe(finalStatus);
    }

    [Fact]
    public void GetLatest_returns_newest_first_and_respects_count()
    {
        var oldest = CreateTransaction(Guid.NewGuid(), TransactionStatus.Pending, BaseTime);
        var newest = CreateTransaction(Guid.NewGuid(), TransactionStatus.Pending, BaseTime.AddMinutes(2));
        var middle = CreateTransaction(Guid.NewGuid(), TransactionStatus.Pending, BaseTime.AddMinutes(1));
        _repository.Upsert(oldest);
        _repository.Upsert(newest);
        _repository.Upsert(middle);

        _repository.GetLatest(10).ShouldBe([newest, middle, oldest]);
        _repository.GetLatest(2).ShouldBe([newest, middle]);
    }

    [Fact]
    public void Exceeding_capacity_removes_the_oldest_transaction()
    {
        var repository = new InMemoryTransactionRepository(capacity: 3);
        var oldest = CreateTransaction(Guid.NewGuid(), TransactionStatus.Pending, BaseTime);
        var second = CreateTransaction(Guid.NewGuid(), TransactionStatus.Pending, BaseTime.AddMinutes(1));
        var third = CreateTransaction(Guid.NewGuid(), TransactionStatus.Pending, BaseTime.AddMinutes(2));
        var fourth = CreateTransaction(Guid.NewGuid(), TransactionStatus.Pending, BaseTime.AddMinutes(3));
        // Inserted out of order: eviction goes by timestamp, not by arrival.
        repository.Upsert(second);
        repository.Upsert(oldest);
        repository.Upsert(third);

        var result = repository.Upsert(fourth);

        result.ShouldBe(UpsertResult.Created);
        repository.GetLatest(10).ShouldBe([fourth, third, second]);
    }

    [Fact]
    public void Updating_an_existing_id_at_capacity_evicts_nothing()
    {
        var repository = new InMemoryTransactionRepository(capacity: 2);
        var first = CreateTransaction(Guid.NewGuid(), TransactionStatus.Pending, BaseTime);
        var second = CreateTransaction(Guid.NewGuid(), TransactionStatus.Pending, BaseTime.AddMinutes(1));
        repository.Upsert(first);
        repository.Upsert(second);

        var update = first with { Status = TransactionStatus.Completed, Timestamp = BaseTime.AddMinutes(2) };
        repository.Upsert(update);

        repository.GetLatest(10).ShouldBe([update, second]);
    }

    [Fact]
    public void GetLatest_returns_a_snapshot_unaffected_by_later_writes()
    {
        var first = CreateTransaction(Guid.NewGuid(), TransactionStatus.Pending, BaseTime);
        _repository.Upsert(first);

        var snapshot = _repository.GetLatest(10);
        _repository.Upsert(CreateTransaction(Guid.NewGuid(), TransactionStatus.Pending, BaseTime.AddMinutes(1)));
        _repository.Upsert(first with { Status = TransactionStatus.Failed, Timestamp = BaseTime.AddMinutes(2) });

        snapshot.ShouldBe([first]);
    }

    private static Transaction CreateTransaction(Guid id, TransactionStatus status, DateTimeOffset timestamp) =>
        new(id, 1500.50m, "USD", status, timestamp);
}
